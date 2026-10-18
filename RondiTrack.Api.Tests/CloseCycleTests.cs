using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using RondiTrack.Api.Jobs;
using RondiTrack.Api.Tests.TestSupport;
using RondiTrack.Domain;
using RondiTrack.Infrastructure.Data;

namespace RondiTrack.Api.Tests;

// 6.4: the cycle-close job, with a fake clock. Each test seeds its own stokvels, so they never interfere.
[Collection("Postgres collection")]
public class CycleCloseTests(PostgresApiFactory factory)
{
    private static readonly TimeSpan Sast = TimeSpan.FromHours(2);        // Africa/Johannesburg: UTC+2, no daylight saving
    private static readonly DateOnly EndDate = new(2031, 3, 25);          // "the cycle ends on the 25th"
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Africa/Johannesburg");

    // ---------- helpers ----------

    private static DateTimeOffset SastTime(DateOnly date, int hour, int minute) =>
        new DateTimeOffset(date.ToDateTime(new TimeOnly(hour, minute)), Sast).ToUniversalTime();

    private CycleCloseJob NewJob(TimeProvider clock) => new(
        factory.Services.GetRequiredService<IServiceScopeFactory>(),
        clock,
        Options.Create(new CycleCloseOptions()),
        factory.Services.GetRequiredService<JobMetrics>(),
        NullLogger<CycleCloseJob>.Instance);

    // A stokvel (R100 target) whose members have paid the given amounts (0 = paid nothing), with one cycle ending on endsOn.
    private async Task<(Guid StokvelId, Guid CycleId, TestUser[] Members)> SeedAsync(DateOnly endsOn, params decimal[] paid)
    {
        var members = new List<TestUser>();
        foreach (var _ in paid) members.Add(await factory.CreateUserAsync());
        var stokvelId = await factory.CreateStokvelAsync(members.Select(m => (m, StokvelMemberRole.Member)).ToArray());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
        var cycle = ContributionCycle.Create(stokvelId, $"T-{Guid.NewGuid():N}"[..12], 100m, endsOn).Value;
        db.ContributionCycles.Add(cycle);
        for (var i = 0; i < paid.Length; i++)
            if (paid[i] > 0)
                db.Contributions.Add(Contribution.Create(stokvelId, members[i].Id, cycle.Id, paid[i]).Value);
        await db.SaveChangesAsync();
        return (stokvelId, cycle.Id, members.ToArray());
    }

    private async Task<List<ContributionCycle>> CyclesOf(Guid stokvelId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
        return await db.ContributionCycles.AsNoTracking().Where(c => c.StokvelId == stokvelId).OrderBy(c => c.EndsOn).ToListAsync();
    }

    private async Task<List<ArrearsRecord>> ArrearsOf(Guid stokvelId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
        return await db.ArrearsRecords.AsNoTracking().Where(a => a.StokvelId == stokvelId).OrderBy(a => a.UserId).ToListAsync();
    }

    private async Task<string> SnapshotOf(Guid stokvelId)
    {
        var cycles = await CyclesOf(stokvelId);
        var arrears = await ArrearsOf(stokvelId);
        return string.Join("|", cycles.Select(c => $"{c.Id}:{c.Status}:{c.EndsOn}:{c.ClosedAt:O}"))
             + "#" + string.Join("|", arrears.Select(a => $"{a.Id}:{a.UserId}:{a.ShortfallCents}"));
    }

    // ---------- cycle close ----------

    [Fact]
    public async Task An_ended_cycle_is_closed_underpayers_get_arrears_in_cents_and_the_next_cycle_opens()
    {
        var (stokvelId, cycleId, m) = await SeedAsync(EndDate, 100m, 40.50m, 0m);   // full, short by R59.50, nothing
        var clock = new FakeTimeProvider(SastTime(EndDate.AddDays(1), 0, 0));

        await NewJob(clock).RunOnceAsync();

        var cycles = await CyclesOf(stokvelId);
        Assert.Equal(2, cycles.Count);
        var closed = cycles.Single(c => c.Id == cycleId);
        Assert.Equal(ContributionCycleStatus.Closed, closed.Status);
        Assert.NotNull(closed.ClosedAt);
        var next = cycles.Single(c => c.Id != cycleId);
        Assert.Equal(ContributionCycleStatus.Open, next.Status);
        Assert.Equal(new DateOnly(2031, 4, 25), next.EndsOn);

        var arrears = await ArrearsOf(stokvelId);
        Assert.Equal(2, arrears.Count);
        Assert.Equal(5950, arrears.Single(a => a.UserId == m[1].Id).ShortfallCents);
        Assert.Equal(10000, arrears.Single(a => a.UserId == m[2].Id).ShortfallCents);
        Assert.DoesNotContain(arrears, a => a.UserId == m[0].Id);   // paid in full: no arrears
    }

    [Fact]
    public async Task A_cycle_that_ends_tomorrow_is_not_touched()
    {
        var (stokvelId, cycleId, _) = await SeedAsync(EndDate, 0m, 0m);
        var clock = new FakeTimeProvider(SastTime(EndDate.AddDays(-1), 12, 0));

        await NewJob(clock).RunOnceAsync();

        var cycles = await CyclesOf(stokvelId);
        Assert.Single(cycles);
        Assert.Equal(ContributionCycleStatus.Open, cycles[0].Status);
        Assert.Empty(await ArrearsOf(stokvelId));
    }

    // ---------- the time zone boundary ----------

    [Fact]
    public async Task The_cycle_closes_at_midnight_SAST_not_midnight_UTC()
    {
        var (stokvelId, _, _) = await SeedAsync(EndDate, 0m);
        var clock = new FakeTimeProvider(SastTime(EndDate, 23, 59));
        var job = NewJob(clock);

        await job.RunOnceAsync();
        Assert.Equal(ContributionCycleStatus.Open, (await CyclesOf(stokvelId)).Single().Status);   // 23:59 SAST: still open

        clock.SetUtcNow(SastTime(EndDate.AddDays(1), 0, 0));
        await job.RunOnceAsync();
        Assert.Equal(2, (await CyclesOf(stokvelId)).Count);                                        // 00:00 SAST: closed, next opened
        Assert.Equal(ContributionCycleStatus.Closed, (await CyclesOf(stokvelId)).First().Status);

        // The README's promise: a cycle ending on the 25th closes at 22:00 UTC on the 25th.
        Assert.Equal(new DateTimeOffset(2031, 3, 25, 22, 0, 0, TimeSpan.Zero), CycleCalendar.ClosesAtUtc(EndDate, Zone));
    }

    // ---------- exactly once ----------

    [Fact]
    public async Task Running_the_job_twice_gives_exactly_the_state_of_running_it_once()
    {
        var (stokvelId, _, _) = await SeedAsync(EndDate, 100m, 40m, 0m);
        var clock = new FakeTimeProvider(SastTime(EndDate.AddDays(1), 9, 0));
        var job = NewJob(clock);

        await job.RunOnceAsync();
        var afterOne = await SnapshotOf(stokvelId);
        await job.RunOnceAsync();
        var afterTwo = await SnapshotOf(stokvelId);

        Assert.Equal(afterOne, afterTwo);
        Assert.Equal(2, (await CyclesOf(stokvelId)).Count);
        Assert.Equal(2, (await ArrearsOf(stokvelId)).Count);
    }

    [Fact]
    public async Task Two_runs_at_the_same_time_close_each_cycle_once_and_record_each_members_arrears_once()
    {
        var seeded = new List<Guid>();
        for (var i = 0; i < 6; i++) seeded.Add((await SeedAsync(EndDate, 100m, 40m, 0m)).StokvelId);   // many stokvels = real overlap
        var clock = new FakeTimeProvider(SastTime(EndDate.AddDays(1), 0, 5));

        // Two "replicas": two job instances started together. Each call creates its own scopes.
        await Task.WhenAll(NewJob(clock).RunOnceAsync(), NewJob(clock).RunOnceAsync());

        foreach (var stokvelId in seeded)
        {
            var cycles = await CyclesOf(stokvelId);
            Assert.Equal(2, cycles.Count);                                               // one closed + exactly one next
            Assert.Equal(1, cycles.Count(c => c.Status == ContributionCycleStatus.Closed));
            Assert.Equal(2, (await ArrearsOf(stokvelId)).Count);                         // two underpayers, each recorded once
        }
    }

    // ---------- catch-up ----------

    [Fact]
    public async Task A_stokvel_three_cycles_behind_ends_with_one_closed_cycle_and_one_current_cycle()
    {
        var firstEnd = new DateOnly(2031, 1, 25);
        var (stokvelId, cycleId, m) = await SeedAsync(firstEnd, 100m, 0m);
        var clock = new FakeTimeProvider(SastTime(new DateOnly(2031, 4, 10), 12, 0));   // Jan, Feb and Mar all ended while "down"
        var job = NewJob(clock);

        await job.RunOnceAsync();

        var cycles = await CyclesOf(stokvelId);
        Assert.Equal(2, cycles.Count);                                                  // README promise: closed + ONE current
        Assert.Equal(ContributionCycleStatus.Closed, cycles.Single(c => c.Id == cycleId).Status);
        var current = cycles.Single(c => c.Id != cycleId);
        Assert.Equal(ContributionCycleStatus.Open, current.Status);
        Assert.Equal(new DateOnly(2031, 4, 25), current.EndsOn);                        // first period end on/after 10 April
        var arrears = await ArrearsOf(stokvelId);
        Assert.Equal(m[1].Id, Assert.Single(arrears).UserId);                           // only for the cycle that really existed

        var snapshot = await SnapshotOf(stokvelId);
        await job.RunOnceAsync();
        Assert.Equal(snapshot, await SnapshotOf(stokvelId));                            // a second run changes nothing
    }

    // ---------- isolation ----------

    [Fact]
    public async Task One_stokvel_that_fails_does_not_stop_another_and_the_job_does_not_throw()
    {
        var clock = new FakeTimeProvider(SastTime(EndDate.AddDays(1), 0, 30));
        var poison = await SeedAsync(EndDate, 100m, 0m);
        var healthy = await SeedAsync(EndDate, 100m, 0m);

        // An impossible state, inserted directly: this OPEN cycle already has an arrears row for the member the job will
        // assess. The job's insert then violates the unique index, so that stokvel's transaction must roll back.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
            db.ArrearsRecords.Add(ArrearsRecord.Create(poison.StokvelId, poison.CycleId, poison.Members[1].Id, 10000, clock.GetUtcNow()));
            await db.SaveChangesAsync();
        }

        var result = await NewJob(clock).RunOnceAsync();     // must not throw

        Assert.True(result.StokvelsFailed >= 1);
        Assert.Equal(ContributionCycleStatus.Closed, (await CyclesOf(healthy.StokvelId)).First().Status);   // the healthy one closed
        Assert.Equal(2, (await CyclesOf(healthy.StokvelId)).Count);

        var poisoned = await CyclesOf(poison.StokvelId);                                                    // the failed one is untouched
        Assert.Single(poisoned);
        Assert.Equal(ContributionCycleStatus.Open, poisoned[0].Status);
        Assert.Single(await ArrearsOf(poison.StokvelId));
    }
}
