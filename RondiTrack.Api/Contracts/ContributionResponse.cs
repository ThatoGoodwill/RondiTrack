using RondiTrack.Api.Domain;

namespace RondiTrack.Api.Contracts;

// OUTGOING shape of a contribution. FromEntity is the ONE place that decides
// what a Contribution looks like once it leaves the server.
public sealed record ContributionResponse(
    Guid Id, Guid StokvelId, Guid UserId, Guid ContributionCycleId, decimal Amount, DateTimeOffset RecordedAt)
{
    public static ContributionResponse FromEntity(Contribution c) =>
        new(c.Id, c.StokvelId, c.UserId, c.ContributionCycleId, c.Amount, c.RecordedAt);
}
