using Microsoft.EntityFrameworkCore;
using RondiTrack.Domain;
using RondiTrack.Domain.Repositories;

namespace RondiTrack.Infrastructure.Data;

public sealed class EfContributionRepository(RondiTrackDbContext context) : IContributionRepository
{
    public async Task<IReadOnlyList<Contribution>> GetByStokvelAsync(Guid stokvelId, CancellationToken ct = default) =>
        await context.Contributions.AsNoTracking()
            .Where(c => c.StokvelId == stokvelId)
            .OrderBy(c => c.RecordedAt).ThenBy(c => c.Id)
            .ToListAsync(ct);

    public async Task<bool> ExistsAsync(Guid stokvelId, Guid userId, Guid contributionCycleId, CancellationToken ct = default) =>
        await context.Contributions.AnyAsync(
            c => c.StokvelId == stokvelId && c.UserId == userId && c.ContributionCycleId == contributionCycleId, ct);

    public async Task AddAsync(Contribution contribution, CancellationToken ct = default)
    {
        context.Contributions.Add(contribution);
        await context.SaveChangesAsync(ct);
    }
}
