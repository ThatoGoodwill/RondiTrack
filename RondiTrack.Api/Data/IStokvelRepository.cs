using RondiTrack.Api.Domain;
namespace RondiTrack.Api.Data;

public interface IStokvelRepository
{
    Task<IReadOnlyList<Stokvel>> GetAllAsync(CancellationToken ct = default);
    Task<Stokvel?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Stokvel stokvel, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task<bool> HasMemberAsync(Guid userId, CancellationToken ct = default);
}