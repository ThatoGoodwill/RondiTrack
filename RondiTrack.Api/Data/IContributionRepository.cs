using RondiTrack.Api.Domain;
namespace RondiTrack.Api.Data;
public interface IContributionRepository
{
    Task<IReadOnlyList<Contribution>> GetByStokvelAsync(Guid stokvelId, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid stokvelId, Guid userId, string cycle, CancellationToken ct = default);
    Task AddAsync(Contribution contribution, CancellationToken ct = default);
}