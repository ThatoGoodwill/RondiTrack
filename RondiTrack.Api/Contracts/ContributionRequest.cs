namespace RondiTrack.Api.Contracts;
public sealed record ContributionRequest(Guid UserId, string? Cycle, decimal Amount);
