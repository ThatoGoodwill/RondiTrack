using RondiTrack.Api.Domain;

namespace RondiTrack.Api.Data;

// Storage abstraction for contribution payments. Stores and fetches; decides nothing.
public interface IContributionRepository
{
    Task<IReadOnlyList<Contribution>> GetByStokvelAsync(Guid stokvelId, CancellationToken ct = default);

    // CHANGED IN 4.3: compares the real cycle id, not a text label.
    // Answers: "has this member already paid for this cycle?"
    Task<bool> ExistsAsync(Guid stokvelId, Guid userId, Guid contributionCycleId, CancellationToken ct = default);

    Task AddAsync(Contribution contribution, CancellationToken ct = default);
}
