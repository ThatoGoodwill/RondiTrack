using Microsoft.EntityFrameworkCore;
using RondiTrack.Api.Contracts;
using RondiTrack.Domain;
using RondiTrack.Infrastructure.Data;

namespace RondiTrack.Api.Services;

public interface IHomeService
{
    Task<HomeResponse> GetHomeAsync(Guid userId, CancellationToken ct = default);
}

// Every calculation happens HERE, on the server, with TimeProvider. The browser only draws what it receives.
// Three queries in total, however many stokvels the caller belongs to (no query per stokvel).
public sealed class HomeService(RondiTrackDbContext db, TimeProvider clock) : IHomeService
{
    public async Task<HomeResponse> GetHomeAsync(Guid userId, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow();

        // 1. the stokvels the caller belongs to, with the caller's role in each
        var memberships = await (
            from m in db.StokvelMembers.AsNoTracking()
            where m.UserId == userId
            join s in db.Stokvels.AsNoTracking() on m.StokvelId equals s.Id
            orderby s.Name
            select new { s.Id, s.Name, s.Frequency, m.Role }).ToListAsync(ct);

        if (memberships.Count == 0) return new HomeResponse([]);      // no stokvels = empty list, never a 404

        // 2. the current cycle of each stokvel = its newest OPEN cycle
        var stokvelIds = memberships.Select(m => m.Id).ToList();
        var openCycles = await db.ContributionCycles.AsNoTracking()
            .Where(c => stokvelIds.Contains(c.StokvelId) && c.Status == ContributionCycleStatus.Open)
            .ToListAsync(ct);
        var currentByStokvel = openCycles
            .GroupBy(c => c.StokvelId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.CreatedAt).First());

        // 3. what the CALLER paid in those cycles (only the caller's own rows are read)
        var cycleIds = currentByStokvel.Values.Select(c => c.Id).ToList();
        var paidByCycle = await db.Contributions.AsNoTracking()
            .Where(c => c.UserId == userId && cycleIds.Contains(c.ContributionCycleId))
            .GroupBy(c => c.ContributionCycleId)
            .Select(g => new { CycleId = g.Key, Total = g.Sum(c => c.Amount) })
            .ToDictionaryAsync(x => x.CycleId, x => x.Total, ct);

        var stokvels = memberships.Select(m =>
        {
            if (!currentByStokvel.TryGetValue(m.Id, out var cycle))
                return new HomeStokvelResponse(m.Id, m.Name, m.Role.ToString(), null, null);

            var paid = paidByCycle.GetValueOrDefault(cycle.Id);
            var owed = Math.Max(0m, cycle.TargetAmount - paid);

            return new HomeStokvelResponse(
                m.Id, m.Name, m.Role.ToString(),
                new HomeCycleResponse(cycle.Id, cycle.Label, ToCents(paid), ToCents(owed)),
                NextPayoutDue(cycle.CreatedAt, m.Frequency, now));
        }).ToList();

        return new HomeResponse(stokvels);
    }

    // Amounts have at most 2 decimals, so this is exact.
    private static long ToCents(decimal rand) => (long)decimal.Round(rand * 100m, 0, MidpointRounding.AwayFromZero);

    // A cycle has no payout date of its own, so the next payout falls one period after the cycle started
    // (a week, a fortnight or a month, from the stokvel's frequency). If that moment has already passed, the next
    // period boundary after "now" is used. Always UTC.
    private static DateTime NextPayoutDue(DateTimeOffset cycleStart, ContributionFrequency frequency, DateTimeOffset now)
    {
        DateTimeOffset At(int periods) => frequency switch
        {
            ContributionFrequency.Weekly => cycleStart.AddDays(7 * periods),
            ContributionFrequency.Fortnightly => cycleStart.AddDays(14 * periods),
            _ => cycleStart.AddMonths(periods)
        };

        var n = 1;
        var due = At(n);
        while (due <= now && n < 1000) due = At(++n);
        return due.UtcDateTime;
    }
}