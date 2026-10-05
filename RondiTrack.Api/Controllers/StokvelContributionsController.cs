using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RondiTrack.Api.Contracts;
using RondiTrack.Api.Data;
using RondiTrack.Api.Domain;
using RondiTrack.Api.Exceptions;
using RondiTrack.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace RondiTrack.Api.Controllers;

[ApiController]
[Route("api/stokvels/{stokvelId:guid}/contributions")]
public class StokvelContributionsController(
    IStokvelRepository stokvels,
    IContributionRepository contributions,
    IContributionService contributionService,
     RondiTrackDbContext context) : ApiControllerBase
{
    [HttpGet]
    [EndpointSummary("List a stokvel's recorded contributions")]
    [EndpointDescription("Returns every contribution recorded for this stokvel, across all cycles.")]
    [ProducesResponseType<List<ContributionResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ContributionResponse>>> GetAll(Guid stokvelId, CancellationToken ct)
    {
        _ = await stokvels.GetByIdAsync(stokvelId, ct)
            ?? throw new NotFoundException("stokvel.not_found", $"No stokvel with id {stokvelId} exists.");

        var list = await contributions.GetByStokvelAsync(stokvelId, ct);
        return Ok(list.Select(ContributionResponse.FromEntity).ToList());
    }

    [HttpPost]
    [EndpointSummary("Record an idempotent contribution payment")]
    [EndpointDescription(
        "Records a member's payment for a specific contribution cycle. REQUIRES an " +
        "Idempotency-Key header. Safe to retry: sending the exact same key and body again " +
        "returns the original response rather than recording a second payment. Reusing the " +
        "same key with a DIFFERENT body is rejected with 409. Fails with 422 if the user is " +
        "not a member of this stokvel or the cycle does not belong to it, and with 409 if this " +
        "member has already paid for this cycle.")]
    [ProducesResponseType<ContributionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
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

        if (outcome.WasReplayed)
            Response.Headers["Idempotent-Replayed"] = "true";

        return StatusCode(StatusCodes.Status201Created, outcome.Response);
    }

        // DELIBERATELY NAIVE (Step 6): one query for the list, then one extra query per row.
   [HttpGet("/api/stokvels/{stokvelId:guid}/cycles/{cycleId:guid}/contributions")]
   public async Task<ActionResult<List<object>>> GetByCycle(Guid stokvelId, Guid cycleId, CancellationToken ct)
   {
      var contributions = await context.Contributions
        .Where(c => c.StokvelId == stokvelId && c.ContributionCycleId == cycleId)
        .Include(c => c.User)
        .AsNoTracking()
        .ToListAsync(ct);

      var results = contributions
        .Select(c => (object)new
        {
            c.Id,
            c.Amount,
            UserName = c.User!.FirstName + " " + c.User.LastName
        })
        .ToList();

      return Ok(results);
    }
}
