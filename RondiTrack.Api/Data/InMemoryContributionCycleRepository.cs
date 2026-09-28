using System.Collections.Concurrent;
using RondiTrack.Api.Domain;

namespace RondiTrack.Api.Data;

public sealed class InMemoryContributionCycleRepository : IContributionCycleRepository
{
    private readonly ConcurrentDictionary<Guid, ContributionCycle> _cycles = new();

    public Task<IReadOnlyList<ContributionCycle>> GetByStokvelAsync(Guid stokvelId, CancellationToken ct = default)
    {
        IReadOnlyList<ContributionCycle> list = _cycles.Values
            .Where(c => c.StokvelId == stokvelId)
            .OrderBy(c => c.Label)
            .ToList();
        return Task.FromResult(list);
    }

    public Task<ContributionCycle?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        _cycles.TryGetValue(id, out var cycle);
        return Task.FromResult<ContributionCycle?>(cycle);
    }

    public Task AddAsync(ContributionCycle cycle, CancellationToken ct = default)
    {
        _cycles[cycle.Id] = cycle;
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(_cycles.TryRemove(id, out _));
}
