using RondiTrack.Api.Domain;

namespace RondiTrack.Api.Data;

public interface IStokvelMemberRepository
{
    Task<StokvelMember?> GetAsync(Guid stokvelId, Guid userId, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid stokvelId, Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<StokvelMember>> GetByStokvelAsync(Guid stokvelId, CancellationToken ct = default);
    Task AddAsync(StokvelMember member, CancellationToken ct = default);
    Task<bool> RemoveAsync(Guid stokvelId, Guid userId, CancellationToken ct = default);
}