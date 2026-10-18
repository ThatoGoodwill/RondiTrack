namespace RondiTrack.Domain;

// A member's shortfall for ONE closed cycle, in cents. A financial record: written once when the cycle closes.
// The unique index on (ContributionCycleId, UserId) means a member can never be put into arrears twice for a cycle.
public sealed class ArrearsRecord
{
    public Guid Id { get; private set; }
    public Guid StokvelId { get; private set; }
    public Guid ContributionCycleId { get; private set; }
    public Guid UserId { get; private set; }
    public long ShortfallCents { get; private set; }
    public DateTimeOffset RecordedAt { get; private set; }

    private ArrearsRecord() { }   // EF Core

    public static ArrearsRecord Create(Guid stokvelId, Guid cycleId, Guid userId, long shortfallCents, DateTimeOffset recordedAt)
    {
        if (shortfallCents <= 0)
            throw new ArgumentOutOfRangeException(nameof(shortfallCents), "A shortfall must be greater than zero.");

        return new ArrearsRecord
        {
            Id = Guid.CreateVersion7(recordedAt),
            StokvelId = stokvelId,
            ContributionCycleId = cycleId,
            UserId = userId,
            ShortfallCents = shortfallCents,
            RecordedAt = recordedAt
        };
    }
}