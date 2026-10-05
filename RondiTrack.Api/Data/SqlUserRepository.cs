using Microsoft.EntityFrameworkCore;
using RondiTrack.Api.Domain;

namespace RondiTrack.Api.Data;

public sealed class SqlUserRepository(RondiTrackDbContext context) : IUserRepository
{
    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct = default) =>
        await context.Users.AsNoTracking().OrderBy(u => u.LastName).ThenBy(u => u.FirstName).ToListAsync(ct);

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await context.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default) =>
        await context.Users.AnyAsync(u => u.Id == id, ct);

    public async Task<bool> EmailExistsAsync(string email, Guid? excludingUserId = null, CancellationToken ct = default) =>
        await context.Users.AnyAsync(u => u.Email == email && u.Id != excludingUserId, ct);

    public async Task AddAsync(User user, CancellationToken ct = default)
    {
        context.Users.Add(user);
        await context.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var user = await context.Users.FindAsync([id], ct);
        if (user is null) return false;
        context.Users.Remove(user);
        await context.SaveChangesAsync(ct);
        return true;
    }

    // This is not good at all .. Where the hell is the passion for this .. 
    

    public Task SaveChangesAsync(CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }
}
