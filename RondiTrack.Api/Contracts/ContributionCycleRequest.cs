namespace RondiTrack.Api.Contracts;

// INCOMING shape for creating/replacing a ContributionCycle.
// Nullable Label on purpose: the VALIDATOR (not the framework) rejects a missing label.
public sealed record ContributionCycleRequest(string? Label, decimal TargetAmount);
