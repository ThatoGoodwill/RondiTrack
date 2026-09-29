using RondiTrack.Api.Domain;

namespace RondiTrack.Api.Data;

// Storage abstraction for stokvels (members live inside each Stokvel).
public interface IStokvelRepository
{
    Task<IReadOnlyList<Stokvel>> GetAllAsync(CancellationToken ct = default);
    Task<Stokvel?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Stokvel stokvel, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);

    // "Is this user a member of ANY stokvel?" No single Stokvel can answer that,
    // so the repository (which sees them all) does. Used to protect user deletion.
    Task<bool> HasMemberAsync(Guid userId, CancellationToken ct = default);
}
