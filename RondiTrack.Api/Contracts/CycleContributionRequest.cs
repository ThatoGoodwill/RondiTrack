namespace RondiTrack.Api.Contracts;

/// <summary>Whole cents only: the client never sends floating-point rands.</summary>
public sealed record CycleContributionRequest(Guid UserId, long AmountInCents);
