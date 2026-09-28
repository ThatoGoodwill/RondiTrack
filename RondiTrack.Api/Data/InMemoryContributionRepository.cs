using System.Collections.Concurrent;
using RondiTrack.Api.Domain;

namespace RondiTrack.Api.Data;

// In-memory implementation. ConcurrentDictionary because requests run in parallel.
// Task.FromResult / Task.CompletedTask: async in SHAPE (ready for EF Core later)
// without wasting a thread, since nothing here is actually slow.
public sealed class InMemoryContributionRepository : IContributionRepository
{
    private readonly ConcurrentDictionary<Guid, Contribution> _contributions = new();

    public Task<IReadOnlyList<Contribution>> GetByStokvelAsync(Guid stokvelId, CancellationToken ct = default)
    {
        IReadOnlyList<Contribution> list = _contributions.Values
            .Where(c => c.StokvelId == stokvelId)
            .OrderBy(c => c.RecordedAt)
            .ToList();
        return Task.FromResult(list);
    }

    public Task<bool> ExistsAsync(Guid stokvelId, Guid userId, Guid contributionCycleId, CancellationToken ct = default) =>
        Task.FromResult(_contributions.Values.Any(c =>
            c.StokvelId == stokvelId &&
            c.UserId == userId &&
            c.ContributionCycleId == contributionCycleId));

    public Task AddAsync(Contribution contribution, CancellationToken ct = default)
    {
        _contributions[contribution.Id] = contribution;
        return Task.CompletedTask;
    }
}
