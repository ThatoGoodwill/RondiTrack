using System.Collections.Concurrent;
using RondiTrack.Api.Domain;
namespace RondiTrack.Api.Data;
public sealed class InMemoryContributionRepository : IContributionRepository
{
    private readonly ConcurrentDictionary<Guid, Contribution> _contributions = new();
    public Task<IReadOnlyList<Contribution>> GetByStokvelAsync(Guid stokvelId, CancellationToken ct = default)
    {
        IReadOnlyList<Contribution> list = _contributions.Values
            .Where(c => c.StokvelId == stokvelId)
            .OrderBy(c => c.Cycle)
            .ToList();
        return Task.FromResult(list);
    }
    public Task<bool> ExistsAsync(Guid stokvelId, Guid userId, string cycle, CancellationToken ct = default) =>
        Task.FromResult(_contributions.Values.Any(c =>
            c.StokvelId == stokvelId && c.UserId == userId &&
            string.Equals(c.Cycle, cycle, StringComparison.OrdinalIgnoreCase)));
    public Task AddAsync(Contribution contribution, CancellationToken ct = default)
    {
        _contributions[contribution.Id] = contribution;
        return Task.CompletedTask;
    }
}
