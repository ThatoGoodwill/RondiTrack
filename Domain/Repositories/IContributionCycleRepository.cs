namespace RondiTrack.Domain.Repositories;

// Storage abstraction for ContributionCycle. 6.1: PostgreSQL-backed, so Update() on a loaded
// cycle needs SaveChangesAsync.
public interface IContributionCycleRepository
{
    Task<IReadOnlyList<ContributionCycle>> GetByStokvelAsync(Guid stokvelId, CancellationToken ct = default);
    Task<ContributionCycle?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(ContributionCycle cycle, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
