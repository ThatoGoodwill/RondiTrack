using Microsoft.AspNetCore.Mvc;
using RondiTrack.Api.Contracts;
using RondiTrack.Api.Data;
using RondiTrack.Api.Domain;
using RondiTrack.Api.Exceptions;
using RondiTrack.Api.Services;

namespace RondiTrack.Api.Controllers;

[ApiController]
[Route("api/stokvels/{stokvelId:guid}/contributions")]
public class StokvelContributionsController(
    IStokvelRepository stokvels,
    IContributionRepository contributions,
    IContributionService contributionService) : ApiControllerBase
{
    // GET /api/stokvels/{stokvelId}/contributions -> 200, or 404 (stokvel missing)
    // Pure read, no decision -> straight to the repository.
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ContributionResponse>>> GetAll(Guid stokvelId, CancellationToken ct)
    {
        _ = await stokvels.GetByIdAsync(stokvelId, ct)
            ?? throw new NotFoundException("stokvel.not_found", $"No stokvel with id {stokvelId} exists.");

        var list = await contributions.GetByStokvelAsync(stokvelId, ct);
        return Ok(list.Select(ContributionResponse.FromEntity).ToList());
    }

    // POST /api/stokvels/{stokvelId}/contributions   (header: Idempotency-Key)
    //   201 on success AND on an identical retry (same body). 400 / 404 / 409 / 422 on failure.
    [HttpPost]
    public async Task<ActionResult<ContributionResponse>> Record(
        Guid stokvelId,
        ContributionRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        var result = await contributionService.RecordContributionAsync(stokvelId, request, idempotencyKey, ct);

        // The ONE hand-picked exception: a reused key is its own, more specific kind of
        // conflict. Everything else uses the generic bridge below.
        if (!result.IsSuccess && result.Error.Code == ContributionErrorCodes.IdempotencyKeyReused)
            throw new IdempotencyKeyConflictException(result.Error.Code, result.Error.Message);

        var outcome = result.ThrowIfFailure();

        // Optional: tell the client this was a replay (the body is identical either way).
        if (outcome.WasReplayed)
            Response.Headers["Idempotent-Replayed"] = "true";

        return StatusCode(StatusCodes.Status201Created, outcome.Response);
    }
}
