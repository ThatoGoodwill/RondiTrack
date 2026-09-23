using RondiTrack.Api.Domain;
namespace RondiTrack.Api.Contracts;
public sealed record ContributionResponse(Guid Id, Guid StokvelId, Guid UserId, string Cycle,
                                          decimal Amount, DateTimeOffset RecordedAt)
{
    public static ContributionResponse FromEntity(Contribution c) =>
        new(c.Id, c.StokvelId, c.UserId, c.Cycle, c.Amount, c.RecordedAt);
}