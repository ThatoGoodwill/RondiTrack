using RondiTrack.Domain;

namespace RondiTrack.Api.Contracts;

// OUTGOING shape of a ContributionCycle.
public sealed record ContributionCycleResponse(
    Guid Id,
    Guid StokvelId,
    string Label,
    decimal TargetAmount,
    string Status,
    DateOnly? EndsOn,
    long CollectedCents,
    long DueCents)
{
    public static ContributionCycleResponse FromEntity(ContributionCycle c, long collectedCents) =>
        new(
            c.Id,
            c.StokvelId,
            c.Label,
            c.TargetAmount,
            c.Status.ToString(),
            c.EndsOn,
            collectedCents,
            DueCents: Math.Max(0L, ToCents(c.TargetAmount) - collectedCents));

    private static long ToCents(decimal rand) =>
        (long)Math.Round(rand * 100m, MidpointRounding.AwayFromZero);
}