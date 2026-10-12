using Microsoft.EntityFrameworkCore;
using RondiTrack.Domain;
using RondiTrack.Domain.Repositories;

namespace RondiTrack.Infrastructure.Data;

public sealed class EfContributionCycleRepository(RondiTrackDbContext context) : IContributionCycleRepository
{
    public async Task<IReadOnlyList<ContributionCycle>> GetByStokvelAsync(Guid stokvelId, CancellationToken ct = default) =>
        await context.ContributionCycles.AsNoTracking()
            .Where(c => c.StokvelId == stokvelId)
            .OrderBy(c => c.CreatedAt).ThenBy(c => c.Id)
            .ToListAsync(ct);

    // TRACKED: the Replace endpoint calls Update() and then SaveChangesAsync.
    public async Task<ContributionCycle?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await context.ContributionCycles.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task AddAsync(ContributionCycle cycle, CancellationToken ct = default)
    {
        context.ContributionCycles.Add(cycle);
        await context.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var cycle = await context.ContributionCycles.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (cycle is null) return false;
        context.ContributionCycles.Remove(cycle);
        await context.SaveChangesAsync(ct);
        return true;
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => context.SaveChangesAsync(ct);
}
