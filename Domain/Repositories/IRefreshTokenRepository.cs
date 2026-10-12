namespace RondiTrack.Domain.Repositories;

public interface IRefreshTokenRepository
{
    // Tracked: the caller is about to rotate (modify) what it gets back.
    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default);

    Task AddAsync(RefreshToken token, CancellationToken ct = default);

    // Saves the successor AND the now-revoked predecessor in ONE SaveChangesAsync
    // (one implicit transaction). Never one without the other.
    Task SaveRotationAsync(RefreshToken successor, CancellationToken ct = default);

    // Revokes every still-active token in the family with ONE UPDATE statement and commits
    // immediately, so nothing that throws afterwards can roll it back.
    Task RevokeFamilyAsync(Guid familyId, DateTime now, CancellationToken ct = default);
}
