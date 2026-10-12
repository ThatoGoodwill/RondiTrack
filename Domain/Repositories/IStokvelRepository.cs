namespace RondiTrack.Domain.Repositories;

// Storage abstraction for stokvels (members live inside each Stokvel).
// 6.1: backed by PostgreSQL, so changes made to a loaded Stokvel (Update, AddMember,
// RemoveMember) are only saved when the caller calls SaveChangesAsync.
public interface IStokvelRepository
{
    Task<IReadOnlyList<Stokvel>> GetAllAsync(CancellationToken ct = default);
    Task<Stokvel?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Stokvel stokvel, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);

    // "Is this user a member of ANY stokvel?" Used to protect user deletion.
    Task<bool> HasMemberAsync(Guid userId, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
