using Microsoft.EntityFrameworkCore;
using RondiTrack.Domain;
using RondiTrack.Domain.Repositories;

namespace RondiTrack.Infrastructure.Data;

public sealed class EfRefreshTokenRepository(RondiTrackDbContext context) : IRefreshTokenRepository
{
    public async Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default) =>
        await context.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public async Task AddAsync(RefreshToken token, CancellationToken ct = default)
    {
        context.RefreshTokens.Add(token);
        await context.SaveChangesAsync(ct);
    }

    public async Task SaveRotationAsync(RefreshToken successor, CancellationToken ct = default)
    {
        // The predecessor was loaded TRACKED by GetByHashAsync and Rotate() set its RevokedAt,
        // so this single SaveChangesAsync sends an UPDATE (guarded by xmin) and an INSERT in
        // ONE transaction.
        context.RefreshTokens.Add(successor);
        await context.SaveChangesAsync(ct);
    }

    public async Task RevokeFamilyAsync(Guid familyId, DateTime now, CancellationToken ct = default) =>
        // ExecuteUpdateAsync runs immediately as one SQL UPDATE. It does not wait for
        // SaveChangesAsync, and nothing thrown afterwards can roll it back.
        await context.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, (DateTime?)now), ct);
}
