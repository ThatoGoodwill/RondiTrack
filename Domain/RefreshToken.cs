namespace RondiTrack.Domain;

// ---------------------------------------------------------------------------------
// 6.1: ONE refresh token, as the SERVER remembers it.
//
//  - TokenHash, never the token. The raw value exists only in the user's cookie, so a
//    leaked database backup leaks hashes nobody can replay.
//  - FamilyId ties together every token descended from ONE login. Rotation keeps the
//    family; presenting a spent token kills the whole family (reuse detection).
//  - RevokedAt is the revocation storage: a JWT cannot be "un-issued", a refresh token can.
// ---------------------------------------------------------------------------------
public sealed class RefreshToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid FamilyId { get; private set; }
    public string TokenHash { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public Guid? ReplacedByTokenId { get; private set; }

    // Mapped to PostgreSQL's xmin (like Payout.Version): two simultaneous refreshes of the
    // SAME token cannot both succeed.
    public uint Version { get; private set; }

    // For EF Core only.
    private RefreshToken()
    {
        TokenHash = null!;
    }

    private RefreshToken(Guid userId, Guid familyId, string tokenHash, DateTime now, TimeSpan lifetime)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("A refresh token must belong to a user.", nameof(userId));
        if (string.IsNullOrWhiteSpace(tokenHash))
            throw new ArgumentException("Token hash is required.", nameof(tokenHash));

        Id = Guid.CreateVersion7();
        UserId = userId;
        FamilyId = familyId;
        TokenHash = tokenHash;
        CreatedAt = now;
        ExpiresAt = now.Add(lifetime);
    }

    public bool IsActive(DateTime now) => RevokedAt is null && now < ExpiresAt;

    // A fresh login starts a brand-new family.
    public static RefreshToken StartFamily(Guid userId, string tokenHash, DateTime now, TimeSpan lifetime) =>
        new(userId, Guid.NewGuid(), tokenHash, now, lifetime);

    // Rotation: THIS token is spent the moment it is used, and its successor joins the same
    // family. Every refresh token works exactly once.
    public RefreshToken Rotate(string newTokenHash, DateTime now, TimeSpan lifetime)
    {
        if (!IsActive(now))
            throw new InvalidOperationException("Only an active refresh token can be rotated.");

        var successor = new RefreshToken(UserId, FamilyId, newTokenHash, now, lifetime);
        RevokedAt = now;
        ReplacedByTokenId = successor.Id;
        return successor;
    }
}
