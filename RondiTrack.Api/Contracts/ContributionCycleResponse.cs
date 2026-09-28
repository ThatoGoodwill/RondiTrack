using RondiTrack.Api.Domain;

namespace RondiTrack.Api.Contracts;

// OUTGOING shape of a ContributionCycle.
public sealed record ContributionCycleResponse(Guid Id, Guid StokvelId, string Label, decimal TargetAmount)
{
    public static ContributionCycleResponse FromEntity(ContributionCycle c) =>
        new(c.Id, c.StokvelId, c.Label, c.TargetAmount);
}
