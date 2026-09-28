using RondiTrack.Api.Domain;

namespace RondiTrack.Api.Data;

// Storage abstraction for ContributionCycle (NEW in 4.3). No update method is needed:
// Update() changes the tracked object in memory (same reasoning as the Stokvel repository).
public interface IContributionCycleRepository
{
    Task<IReadOnlyList<ContributionCycle>> GetByStokvelAsync(Guid stokvelId, CancellationToken ct = default);
    Task<ContributionCycle?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(ContributionCycle cycle, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
