using RondiTrack.Api.Contracts;
using RondiTrack.Api.Domain;

namespace RondiTrack.Api.Services;

// What a successful call returns. WasReplayed = true means "this is the stored answer
// to an earlier identical request; nothing new was recorded".
public sealed record ContributionOutcome(ContributionResponse Response, bool WasReplayed);

// Shared error codes, so the service and the controller cannot drift apart on spelling.
public static class ContributionErrorCodes
{
    public const string IdempotencyKeyReused = "idempotency.key_reused";
}

public interface IContributionService
{
    Task<Result<ContributionOutcome>> RecordContributionAsync(
        Guid stokvelId, ContributionRequest request, string? idempotencyKey, CancellationToken ct = default);
}
