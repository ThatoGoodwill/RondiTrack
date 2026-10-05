using Microsoft.EntityFrameworkCore;
using RondiTrack.Api.Domain;

namespace RondiTrack.Api.Data;

public sealed class SqlStokvelMemberRepository(RondiTrackDbContext context) : IStokvelMemberRepository
{
    // FindAsync takes the key values in the order the key was declared: (StokvelId, UserId)
    public async Task<StokvelMember?> GetAsync(Guid stokvelId, Guid userId, CancellationToken ct = default) =>
        await context.StokvelMembers.FindAsync([stokvelId, userId], ct);

    public async Task<bool> ExistsAsync(Guid stokvelId, Guid userId, CancellationToken ct = default) =>
        await context.StokvelMembers.AnyAsync(m => m.StokvelId == stokvelId && m.UserId == userId, ct);

    public async Task<IReadOnlyList<StokvelMember>> GetByStokvelAsync(Guid stokvelId, CancellationToken ct = default) =>
        await context.StokvelMembers.AsNoTracking().Where(m => m.StokvelId == stokvelId).ToListAsync(ct);

    public async Task AddAsync(StokvelMember member, CancellationToken ct = default)
    {
        context.StokvelMembers.Add(member);
        await context.SaveChangesAsync(ct);
    }

    public async Task<bool> RemoveAsync(Guid stokvelId, Guid userId, CancellationToken ct = default)
    {
        var member = await context.StokvelMembers.FindAsync([stokvelId, userId], ct);
        if (member is null) return false;
        context.StokvelMembers.Remove(member);
        await context.SaveChangesAsync(ct);
        return true;
    }
}