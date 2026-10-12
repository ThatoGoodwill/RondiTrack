using Microsoft.EntityFrameworkCore;
using RondiTrack.Domain;
using RondiTrack.Domain.Repositories;

namespace RondiTrack.Infrastructure.Data;

// 6.1: users now live in PostgreSQL (Scoped: it holds a DbContext). The registration rule
// "one account per email" is backed by the unique index IX_Users_Email, not only by code.
public sealed class EfUserRepository(RondiTrackDbContext context) : IUserRepository
{
    // Read-only listing: no tracking needed.
    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct = default) =>
        await context.Users.AsNoTracking().OrderBy(u => u.LastName).ThenBy(u => u.FirstName).ToListAsync(ct);

    // TRACKED on purpose: UpdateUserAsync and DeleteUserAsync modify what this returns.
    public async Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await context.Users.FindAsync([id], ct);

    public async Task<bool> ExistsAsync(Guid id, CancellationToken ct = default) =>
        await context.Users.AnyAsync(u => u.Id == id, ct);

    public async Task<bool> EmailExistsAsync(string email, Guid? excludingUserId = null, CancellationToken ct = default)
    {
        var query = context.Users.Where(u => u.Email == email);
        if (excludingUserId is { } id) query = query.Where(u => u.Id != id);
        return await query.AnyAsync(ct);
    }

    // Login only READS the user, so no tracking.
    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalized = User.NormalizeEmail(email);
        if (normalized is null) return null;
        return await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == normalized, ct);
    }

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

    public Task SaveChangesAsync(CancellationToken ct = default) => context.SaveChangesAsync(ct);
}
