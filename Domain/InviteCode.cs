namespace RondiTrack.Domain;

// Only SHA-256 of the random bearer invite code is stored in PostgreSQL.
public sealed class InviteCode
{
    public string CodeHash { get; private set; } = "";
    public Guid StokvelId { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    private InviteCode() { }
    public static InviteCode Create(string hash, Guid stokvelId, Guid createdBy, DateTimeOffset expires) => new()
    {
        CodeHash = hash, StokvelId = stokvelId, CreatedBy = createdBy,
        CreatedAtUtc = DateTimeOffset.UtcNow, ExpiresAtUtc = expires
    };
}
