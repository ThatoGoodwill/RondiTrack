namespace RondiTrack.Domain.Repositories;

// Storage abstraction for users. 6.1 adds GetByEmailAsync (login).
public interface IUserRepository
{
    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct = default);
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);

    // "Is ANOTHER user already using this email?" (excludingUserId lets an update ignore itself.)
    Task<bool> EmailExistsAsync(string email, Guid? excludingUserId = null, CancellationToken ct = default);

    // Read-only lookup by (normalised) email. Used by login.
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);

    Task AddAsync(User user, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
