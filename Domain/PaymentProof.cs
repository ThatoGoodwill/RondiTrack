namespace RondiTrack.Domain;

// Bank documents are private and held in database bytea rather than web-accessible directories.
public sealed class PaymentProof
{
    public Guid Id { get; private set; }
    public Guid StokvelId { get; private set; }
    public Guid ContributionId { get; private set; }
    public Guid UploadedByUserId { get; private set; }
    public string MimeType { get; private set; } = "";
    public byte[] Bytes { get; private set; } = [];
    public DateTimeOffset UploadedAtUtc { get; private set; }
    private PaymentProof() { }
    public static PaymentProof Create(Guid stokvelId, Guid contributionId, Guid userId,
        string mimeType, byte[] bytes) => new()
        {
            Id = Guid.CreateVersion7(), StokvelId = stokvelId,
            ContributionId = contributionId, UploadedByUserId = userId,
            MimeType = mimeType, Bytes = bytes, UploadedAtUtc = DateTimeOffset.UtcNow
        };
    public void Replace(string mimeType, byte[] bytes)
    {
        MimeType = mimeType;
        Bytes = bytes;
        UploadedAtUtc = DateTimeOffset.UtcNow;
    }
}
