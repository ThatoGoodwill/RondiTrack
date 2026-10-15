using RondiTrack.Api.Contracts;
using RondiTrack.Api.Observability;
using RondiTrack.Domain;

namespace RondiTrack.Api.Services;

// Wraps ContributionService (the class that owns the operation) and counts every contribution that was NEWLY recorded.
// A wrapper rather than an edit, so the money logic (idempotency, membership, duplicate payment) stays exactly as
// tested in 5.x, and so there is no telemetry in a controller. Registered in Program.cs.
//
// An idempotent replay returns the stored answer and records nothing, so it is not counted: the counter means
// "payments that entered the system", not "requests received" (the request-rate metric already covers those).
public sealed class InstrumentedContributionService(IContributionService inner, RondiTrackMetrics metrics)
    : IContributionService
{
    public async Task<Result<ContributionOutcome>> RecordContributionAsync(
        Guid stokvelId, ContributionRequest request, string? idempotencyKey, CancellationToken ct = default)
    {
        var result = await inner.RecordContributionAsync(stokvelId, request, idempotencyKey, ct);

        if (result.IsSuccess && !result.Value.WasReplayed)
            metrics.ContributionRecorded();

        return result;
    }
}
