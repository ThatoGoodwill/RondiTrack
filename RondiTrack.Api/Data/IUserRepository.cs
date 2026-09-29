using RondiTrack.Api.Domain;

namespace RondiTrack.Api.Data;

// Storage abstraction for users. Controllers/services depend on THIS interface,
// never on the in-memory class, so swapping in EF Core later changes one line in Program.cs.
// Every method is async (Task) even though memory is instant: the shape is ready for a real database.
public interface IUserRepository
{
    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct = default);
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);

    // "Is ANOTHER user already using this email?" (excludingUserId lets an update ignore itself.)
    Task<bool> EmailExistsAsync(string email, Guid? excludingUserId = null, CancellationToken ct = default);

    Task AddAsync(User user, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
