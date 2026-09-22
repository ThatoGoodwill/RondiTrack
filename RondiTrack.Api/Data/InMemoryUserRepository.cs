using System.Collections.Concurrent;
using RondiTrack.Api.Domain;
namespace RondiTrack.Api.Data;

public sealed class InMemoryUserRepository : IUserRepository
{
    private readonly ConcurrentDictionary<Guid, User> _users = new();

    public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct = default)
    {
        IReadOnlyList<User> all = _users.Values.OrderBy(u => u.LastName).ThenBy(u => u.FirstName).ToList();
        return Task.FromResult(all);
    }
    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
    { _users.TryGetValue(id, out var user); return Task.FromResult<User?>(user); }
    public Task<bool> ExistsAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_users.ContainsKey(id));
    public Task<bool> EmailExistsAsync(string email, Guid? excludingUserId = null, CancellationToken ct = default) =>
        Task.FromResult(_users.Values.Any(u => u.Email == email && u.Id != excludingUserId));
    public Task AddAsync(User user, CancellationToken ct = default) { _users[user.Id] = user; return Task.CompletedTask; }
    public Task<bool> DeleteAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_users.TryRemove(id, out _));
}
