using RondiTrack.Api.Domain;
namespace RondiTrack.Api.Contracts;
public sealed record PayoutResponse(Guid Id, Guid StokvelId, Guid ContributionCycleId, Guid RecipientUserId,
 decimal Amount, DateTimeOffset ProcessedAt)
{
 public static PayoutResponse FromEntity(Payout p) =>
 new(p.Id, p.StokvelId, p.ContributionCycleId, p.RecipientUserId, p.Amount, p.ProcessedAt);
}