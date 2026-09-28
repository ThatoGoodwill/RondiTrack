namespace RondiTrack.Api.Contracts;

// INCOMING shape for POST /api/stokvels/{id}/contributions.
// CHANGED IN 4.3: "Cycle" (free text) became ContributionCycleId (a real cycle's id).
// Pure data holder: no logic, no attributes. Its rules live in ContributionRequestValidator.
public sealed record ContributionRequest(Guid UserId, Guid ContributionCycleId, decimal Amount);
