using RondiTrack.Api.Domain;
namespace RondiTrack.Api.Data;

public interface IUserRepository
{
    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct = default);
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct = default);
    Task<bool> EmailExistsAsync(string email, Guid? excludingUserId = null, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}