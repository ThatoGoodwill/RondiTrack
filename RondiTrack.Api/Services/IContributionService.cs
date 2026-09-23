using RondiTrack.Api.Contracts;
using RondiTrack.Api.Domain;
namespace RondiTrack.Api.Services;
// WasReplayed tells the controller "this exact response was already given once before" -
// useful for logging/testing, though the HTTP response looks the same either way.
public sealed record ContributionOutcome(ContributionResponse Response, bool WasReplayed);
public interface IContributionService
{
    Task<Result<ContributionOutcome>> RecordContributionAsync(
        Guid stokvelId, ContributionRequest request, string? idempotencyKey, CancellationToken ct = default);
}