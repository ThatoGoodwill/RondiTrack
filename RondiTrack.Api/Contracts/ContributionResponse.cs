using RondiTrack.Domain;

namespace RondiTrack.Api.Contracts;

// OUTGOING shape of a contribution. FromEntity is the ONE place that decides
// what a Contribution looks like once it leaves the server.
// Money leaves as integer cents (AmountCents), never as a decimal of rands.
public sealed record ContributionResponse(
    Guid Id,
    Guid StokvelId,
    Guid UserId,
    Guid ContributionCycleId,
    long AmountCents,
    DateTimeOffset RecordedAt,
    string CycleLabel)
{
    public static ContributionResponse FromEntity(Contribution c, string cycleLabel) =>
        new(c.Id, c.StokvelId, c.UserId, c.ContributionCycleId, ToCents(c.Amount), c.RecordedAt, cycleLabel);

    // Same exact-cents conversion as HomeService.ToCents. Amounts have at most 2 decimals, so this is exact.
    public static long ToCents(decimal rand) =>
        (long)decimal.Round(rand * 100m, 0, MidpointRounding.AwayFromZero);
}