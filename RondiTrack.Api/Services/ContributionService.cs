using System.Text.Json;
using RondiTrack.Api.Contracts;
using RondiTrack.Api.Data;
using RondiTrack.Api.Domain;
namespace RondiTrack.Api.Services;
public sealed class ContributionService(
    IStokvelRepository stokvels, IContributionRepository contributions, IIdempotencyStore idempotency)
    : IContributionService
{
    public async Task<Result<ContributionOutcome>> RecordContributionAsync(
        Guid stokvelId, ContributionRequest request, string? idempotencyKey, CancellationToken ct = default)
    {
        // ---- Step A: an Idempotency-Key is mandatory for a money-moving endpoint ---
        if (string.IsNullOrWhiteSpace(idempotencyKey))

            return Result<ContributionOutcome>.Failure(new Error(ErrorType.Validation,
                "contribution.idempotency_key_required", "An Idempotency-Key header is required."));
        // ---- Step B: has this exact key been seen before? Check THIS FIRST, before touching any business data ---
        var requestHash = IdempotencyHasher.Compute(new { stokvelId, request.UserId, request.Cycle, request.Amount });
        var existing = await idempotency.FindAsync(idempotencyKey, ct);
        if (existing is not null)
        {
            if (existing.RequestHash != requestHash)
                return Result<ContributionOutcome>.Failure(new Error(ErrorType.Conflict,
                    "idempotency.key_reused", "This Idempotency-Key was already used with a different request."));
            // Same key, same body: hand back EXACTLY what we said the first time. Do no new work.
            var cached = JsonSerializer.Deserialize<ContributionResponse>(existing.ResponseBodyJson)!;
            return Result<ContributionOutcome>.Success(new ContributionOutcome(cached, WasReplayed: true));
        }
        // ---- Step C: genuinely new attempt -- now the real business rules apply ---
        var stokvel = await stokvels.GetByIdAsync(stokvelId, ct);
        if (stokvel is null)
            return Result<ContributionOutcome>.Failure(new Error(ErrorType.NotFound,
                "stokvel.not_found", $"No stokvel with id {stokvelId} exists."));
        if (!stokvel.HasMember(request.UserId))
            return Result<ContributionOutcome>.Failure(new Error(ErrorType.Unprocessable,
                "contribution.not_a_member", "This user is not a member of this stokvel."));
        if (await contributions.ExistsAsync(stokvelId, request.UserId, request.Cycle ?? "", ct))
            return Result<ContributionOutcome>.Failure(new Error(ErrorType.Conflict,
                "contribution.already_recorded",
                $"A contribution for cycle '{request.Cycle}' has already been recorded for this member."));
        var created = Contribution.Create(stokvelId, request.UserId, request.Cycle, request.Amount);
        if (!created.IsSuccess) return Result<ContributionOutcome>.Failure(created.Error);
        await contributions.AddAsync(created.Value, ct);
        var response = ContributionResponse.FromEntity(created.Value);
        // ---- Step D: remember this key BEFORE returning, so a retry finds it ---
        await idempotency.SaveAsync(
            new IdempotencyRecord(idempotencyKey, requestHash, JsonSerializer.Serialize(response), DateTimeOffset.UtcNow), ct);
        return Result<ContributionOutcome>.Success(new ContributionOutcome(response, WasReplayed: false));
    }
}