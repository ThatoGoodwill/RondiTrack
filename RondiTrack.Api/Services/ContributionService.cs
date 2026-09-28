using System.Text.Json;
using RondiTrack.Api.Contracts;
using RondiTrack.Api.Data;
using RondiTrack.Api.Domain;

namespace RondiTrack.Api.Services;

// ---------------------------------------------------------------------------------
// THE MOST IMPORTANT DECISION IN THE PROJECT: recording a payment safely.
// The idempotency check lives HERE, in the same method as the write it protects.
//
// Order of the checks:
//   A. Idempotency-Key present?                          -> else Validation (400)
//   B. Key already seen?
//        same request  -> return the ORIGINAL response, do nothing new
//        different req -> Conflict "idempotency.key_reused" (controller throws the
//                         specific IdempotencyKeyConflictException -> 409)
//   C. Business rules for a genuinely NEW attempt:
//        stokvel exists?            -> NotFound (404)
//        user is a member?          -> Unprocessable (422)
//        cycle exists for stokvel?  -> Unprocessable (422)
//        already paid this cycle?   -> Conflict (409)
//        amount valid?              -> the Contribution entity decides
//   D. Save the contribution, THEN remember the key + response.
// Failed attempts are not stored, so retrying a failed request re-evaluates it.
// ---------------------------------------------------------------------------------
public sealed class ContributionService(
    IStokvelRepository stokvels,
    IContributionRepository contributions,
    IContributionCycleRepository cycles,
    IIdempotencyStore idempotency) : IContributionService
{
    public async Task<Result<ContributionOutcome>> RecordContributionAsync(
        Guid stokvelId, ContributionRequest request, string? idempotencyKey, CancellationToken ct = default)
    {
        // ---- A. the key is mandatory for a money-moving endpoint ----
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return Fail(ErrorType.Validation, "contribution.idempotency_key_required",
                "An Idempotency-Key header is required.");

        // ---- B. have we seen this key before? (checked FIRST, before any business data) ----
        // The fingerprint covers everything that makes the request "the same request".
        var requestHash = IdempotencyHasher.Compute(
            new { stokvelId, request.UserId, request.ContributionCycleId, request.Amount });

        var existing = await idempotency.FindAsync(idempotencyKey, ct);
        if (existing is not null)
        {
            if (existing.RequestHash != requestHash)
                return Fail(ErrorType.Conflict, ContributionErrorCodes.IdempotencyKeyReused,
                    "This Idempotency-Key was already used with a different request.");

            // Genuine retry: hand back EXACTLY what we answered the first time.
            var cached = JsonSerializer.Deserialize<ContributionResponse>(existing.ResponseBodyJson)!;
            return Result<ContributionOutcome>.Success(new ContributionOutcome(cached, WasReplayed: true));
        }

        // ---- C. genuinely new attempt: business rules ----
        var stokvel = await stokvels.GetByIdAsync(stokvelId, ct);
        if (stokvel is null)
            return Fail(ErrorType.NotFound, "stokvel.not_found", $"No stokvel with id {stokvelId} exists.");

        if (!stokvel.HasMember(request.UserId))
            return Fail(ErrorType.Unprocessable, "contribution.not_a_member",
                "This user is not a member of this stokvel.");

        var cycle = await cycles.GetByIdAsync(request.ContributionCycleId, ct);
        if (cycle is null || cycle.StokvelId != stokvelId)
            return Fail(ErrorType.Unprocessable, "contribution.cycle_not_found",
                "No such contribution cycle exists for this stokvel.");

        if (await contributions.ExistsAsync(stokvelId, request.UserId, request.ContributionCycleId, ct))
            return Fail(ErrorType.Conflict, "contribution.already_recorded",
                "A contribution for this cycle has already been recorded for this member.");

        var created = Contribution.Create(stokvelId, request.UserId, request.ContributionCycleId, request.Amount);
        if (!created.IsSuccess) return Result<ContributionOutcome>.Failure(created.Error);

        // ---- D. write, then remember the key so a retry finds it ----
        await contributions.AddAsync(created.Value, ct);
        var response = ContributionResponse.FromEntity(created.Value);

        await idempotency.SaveAsync(
            new IdempotencyRecord(idempotencyKey, requestHash, JsonSerializer.Serialize(response), DateTimeOffset.UtcNow),
            ct);

        return Result<ContributionOutcome>.Success(new ContributionOutcome(response, WasReplayed: false));
    }

    // Small helper so each failure above is one readable line.
    private static Result<ContributionOutcome> Fail(ErrorType type, string code, string message) =>
        Result<ContributionOutcome>.Failure(new Error(type, code, message));
}
