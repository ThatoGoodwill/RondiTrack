using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RondiTrack.Api.Observability;
using RondiTrack.Domain;
using RondiTrack.Infrastructure.Data;

namespace RondiTrack.Api.Jobs;

public sealed record CycleCloseResult(int CyclesClosed, long ArrearsRecorded, int StokvelsFailed);

// Closes every stokvel cycle that has ended, records arrears, opens the next cycle.
//
//  EXACTLY ONCE. Closing is a conditional UPDATE ("set Closed WHERE Status = Open"). The database lets exactly ONE
//  caller win it, even with two replicas in the same second: the loser's UPDATE waits for the winner's transaction,
//  re-checks the condition, matches 0 rows, and does nothing. Arrears also have a unique index per (cycle, member).
//  ONE TRANSACTION PER STOKVEL. The claim, the arrears and the next cycle commit together or not at all.
//  NEVER TAKES THE API DOWN. Everything is caught per stokvel and per run; a failed stokvel is retried next tick.
//  TIME. "Now" is the injected TimeProvider; "today" is the date in the stokvel's time zone (Africa/Johannesburg).
public sealed class CycleCloseJob(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    IOptions<CycleCloseOptions> options,
    JobMetrics metrics,
    ILogger<CycleCloseJob> logger) : BackgroundService
{
    public const string SpanName = "cycle_close.run";

    private readonly CycleCloseOptions _options = options.Value;
    private readonly TimeZoneInfo _zone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZoneId);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Cycle-close job is disabled (CycleClose__Enabled=false).");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(_options.IntervalMinutes), clock);
        try
        {
            // Run once at startup (this is how it catches up after downtime), then on every tick.
            do { await RunOnceAsync(stoppingToken); }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) { /* the host is shutting down */ }
    }

    // Tests call this directly. It never throws (except when cancelled).
    public async Task<CycleCloseResult> RunOnceAsync(CancellationToken ct = default)
    {
        using var activity = RondiTrackTelemetry.ActivitySource.StartActivity(SpanName);
        int closed = 0, failed = 0;
        long arrears = 0;

        try
        {
            var now = clock.GetUtcNow();
            var today = CycleCalendar.TodayIn(_zone, now);

            List<Guid> stokvelIds;
            using (var scope = scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
                stokvelIds = await db.ContributionCycles.AsNoTracking()
                    .Where(c => c.Status == ContributionCycleStatus.Open && c.EndsOn != null && c.EndsOn < today)
                    .Select(c => c.StokvelId)
                    .Distinct()
                    .ToListAsync(ct);
            }

            foreach (var stokvelId in stokvelIds)
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();   // a fresh DbContext per stokvel: a failure leaves no residue
                    var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
                    var (c, a) = await CloseStokvelAsync(db, stokvelId, now, today, ct);
                    closed += c;
                    arrears += a;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    failed++;
                    metrics.CycleCloseFailed();
                    // Ids only (6.2 rule): never a member's name, phone or ID number.
                    logger.LogError(ex, "Cycle close failed and was rolled back; it is retried on the next run. StokvelId={StokvelId}", stokvelId);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            failed++;
            activity?.SetStatus(ActivityStatusCode.Error);
            logger.LogError(ex, "Cycle-close run failed before any stokvel was processed; it is retried on the next run.");
        }

        activity?.SetTag("ronditrack.job.cycles_closed", closed);
        activity?.SetTag("ronditrack.job.arrears_recorded", arrears);
        activity?.SetTag("ronditrack.job.stokvels_failed", failed);
        logger.LogInformation(
            "Cycle-close run finished. CyclesClosed={CyclesClosed} ArrearsRecorded={ArrearsRecorded} StokvelsFailed={StokvelsFailed}",
            closed, arrears, failed);

        return new CycleCloseResult(closed, arrears, failed);
    }

    private async Task<(int Closed, long Arrears)> CloseStokvelAsync(
        RondiTrackDbContext db, Guid stokvelId, DateTimeOffset now, DateOnly today, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);   // dispose without commit = rollback

        var stokvel = await db.Stokvels.AsNoTracking()
            .Where(s => s.Id == stokvelId)
            .Select(s => new { s.Frequency, s.ContributionAmount })
            .SingleOrDefaultAsync(ct);
        if (stokvel is null) return (0, 0);

        var overdue = await db.ContributionCycles.AsNoTracking()
            .Where(c => c.StokvelId == stokvelId && c.Status == ContributionCycleStatus.Open
                        && c.EndsOn != null && c.EndsOn < today)
            .OrderBy(c => c.EndsOn)
            .Select(c => new { c.Id, EndsOn = c.EndsOn!.Value, c.TargetAmount })
            .ToListAsync(ct);
        if (overdue.Count == 0) return (0, 0);

        var memberIds = await db.StokvelMembers.AsNoTracking()
            .Where(m => m.StokvelId == stokvelId)
            .Select(m => m.UserId)
            .ToListAsync(ct);

        var closed = 0;
        long arrearsCount = 0;
        var lastEnd = default(DateOnly);

        foreach (var cycle in overdue)
        {
            // THE GATE. Only one caller, ever, moves this cycle from Open to Closed.
            var claimed = await db.ContributionCycles
                .Where(c => c.Id == cycle.Id && c.Status == ContributionCycleStatus.Open)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.Status, ContributionCycleStatus.Closed)
                    .SetProperty(c => c.ClosedAt, (DateTimeOffset?)now), ct);
            if (claimed == 0) continue;   // another replica closed it first: nothing for us to do

            var paid = await db.Contributions.AsNoTracking()
                .Where(c => c.ContributionCycleId == cycle.Id)
                .GroupBy(c => c.UserId)
                .Select(g => new { UserId = g.Key, Total = g.Sum(c => c.Amount) })
                .ToDictionaryAsync(x => x.UserId, x => x.Total, ct);

            var targetCents = ToCents(cycle.TargetAmount);
            foreach (var memberId in memberIds)   // EVERY member is assessed: nobody is skipped
            {
                var shortfall = targetCents - ToCents(paid.GetValueOrDefault(memberId));
                if (shortfall <= 0) continue;     // paid in full (or more): no arrears
                db.ArrearsRecords.Add(ArrearsRecord.Create(stokvelId, cycle.Id, memberId, shortfall, now));
                arrearsCount++;
            }

            closed++;
            lastEnd = cycle.EndsOn;   // ordered ascending, so this ends as the latest closed cycle's end date
        }

        if (closed == 0) return (0, 0);   // lost the race on every cycle: the transaction rolls back, nothing changed

        // CATCH-UP RULE: open exactly ONE next cycle, the first period whose end date is today or later.
        // Missed periods are not backfilled: nobody could pay while the API was down, so they get no phantom arrears.
        var nextEnd = CycleCalendar.NextEndOnOrAfter(stokvel.Frequency, lastEnd, today);
        var alreadyThere = await db.ContributionCycles.AnyAsync(c => c.StokvelId == stokvelId && c.EndsOn == nextEnd, ct);
        if (!alreadyThere)
        {
            var next = ContributionCycle.Create(
                stokvelId, CycleCalendar.LabelFor(stokvel.Frequency, nextEnd), stokvel.ContributionAmount, nextEnd, now);
            if (!next.IsSuccess)
                throw new InvalidOperationException($"The next cycle would be invalid: {next.Error.Code}");
            db.ContributionCycles.Add(next.Value);
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        for (var i = 0; i < closed; i++) metrics.CycleClosed();
        metrics.ArrearsRecorded(arrearsCount);
        logger.LogInformation("Stokvel cycle(s) closed. StokvelId={StokvelId} Cycles={Cycles} Arrears={Arrears}",
            stokvelId, closed, arrearsCount);

        return (closed, arrearsCount);
    }

    // Amounts have at most 2 decimals, so this is exact.
    private static long ToCents(decimal rand) => (long)decimal.Round(rand * 100m, 0, MidpointRounding.AwayFromZero);
}