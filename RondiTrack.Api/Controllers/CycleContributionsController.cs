using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RondiTrack.Api.Authorization;
using RondiTrack.Api.Contracts;
using RondiTrack.Api.Frontend;
using RondiTrack.Api.Services;
using RondiTrack.Domain.Exceptions;

namespace RondiTrack.Api.Controllers;

/// <summary>Typed, cycle-scoped contribution endpoint for browser and server actions.</summary>
[ApiController]
[Route(ApiRoutes.Base + "/stokvels/{stokvelId:guid}/cycles/{cycleId:guid}/contributions")]
public sealed class CycleContributionsController(IContributionService service) : ApiControllerBase
{
    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Contributions)]
    [EndpointSummary("Record whole cents with one Idempotency-Key per payment intent")]
    [ProducesResponseType<ContributionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ContributionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ContributionResponse>> Record(
        Guid stokvelId, Guid cycleId, CycleContributionRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey, CancellationToken ct)
    {
        if (request.AmountInCents <= 0 || request.AmountInCents > 9_000_000_000_000L)
            throw new ValidationFailedException("contribution.invalid_amount", "Enter a positive amount in whole cents.");
        await this.EnsureCanRecordContributionAsync(stokvelId, request.UserId);
        var decimalRands = request.AmountInCents / 100m;
        var outcome = (await service.RecordContributionAsync(
            stokvelId, new ContributionRequest(request.UserId, cycleId, decimalRands), idempotencyKey, ct));
        if (!outcome.IsSuccess && outcome.Error.Code == ContributionErrorCodes.IdempotencyKeyReused)
            throw new IdempotencyKeyConflictException(outcome.Error.Code, outcome.Error.Message);
        var saved = outcome.ThrowIfFailure();
        if (saved.WasReplayed)
        {
            Response.Headers["Idempotency-Replayed"] = "true";
            return Ok(saved.Response);
        }
        return Created($"{ApiRoutes.Base}/stokvels/{stokvelId}/contributions/{saved.Response.Id}", saved.Response);
    }
}
