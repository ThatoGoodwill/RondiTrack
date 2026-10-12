using Microsoft.EntityFrameworkCore;
using RondiTrack.Domain;
using RondiTrack.Domain.Repositories;

namespace RondiTrack.Infrastructure.Data;

// 6.1: stokvels (with their members) now live in PostgreSQL, so membership is ONE fact in ONE
// place and the authorization handler reads the same rows the API writes.
public sealed class EfStokvelRepository(RondiTrackDbContext context) : IStokvelRepository
{
    public async Task<IReadOnlyList<Stokvel>> GetAllAsync(CancellationToken ct = default) =>
        await context.Stokvels.AsNoTracking().Include(s => s.Members).OrderBy(s => s.Name).ToListAsync(ct);

    // TRACKED, members included: callers call Update/AddMember/RemoveMember and then SaveChangesAsync.
    public async Task<Stokvel?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await context.Stokvels.Include(s => s.Members).FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task AddAsync(Stokvel stokvel, CancellationToken ct = default)
    {
        context.Stokvels.Add(stokvel);   // the whole graph: the stokvel AND any members already added
        await context.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var stokvel = await context.Stokvels.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (stokvel is null) return false;
        context.Stokvels.Remove(stokvel);   // memberships go with it (cascade)
        await context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> HasMemberAsync(Guid userId, CancellationToken ct = default) =>
        await context.StokvelMembers.AnyAsync(m => m.UserId == userId, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => context.SaveChangesAsync(ct);
}
