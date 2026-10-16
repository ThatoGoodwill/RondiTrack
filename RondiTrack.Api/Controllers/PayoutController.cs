using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Api.Authorization;
using RondiTrack.Api.Contracts;
using RondiTrack.Api.Services;
using RondiTrack.Domain;
using RondiTrack.Domain.Exceptions;
using RondiTrack.Infrastructure.Data;
using Microsoft.AspNetCore.RateLimiting;
using RondiTrack.Api.Frontend;

namespace RondiTrack.Api.Controllers;

[ApiController]
[Route(ApiRoutes.Base + "/stokvels/{stokvelId:guid}/cycles/{cycleId:guid}/payout")]
public class PayoutsController(IPayoutService payoutService) : ApiControllerBase
{
    [HttpPost]
    [EnableRateLimiting(RateLimitPolicies.Payouts)]
    [StokvelAccess(Policies.StokvelTreasurer)]   // treasurer or chairperson only
    [EndpointSummary("Process the next payout for a stokvel's contribution cycle")]
    [EndpointDescription("Determines the next eligible recipient by join order, creates a Payout record, " +
                          "and marks the cycle as paid out -- both writes succeed or fail together.")]
    [ProducesResponseType<PayoutResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PayoutResponse>> Process(Guid stokvelId, Guid cycleId, CancellationToken ct)
    {
        var payout = (await payoutService.ProcessNextPayoutAsync(stokvelId, cycleId, ct)).ThrowIfFailure();
        return StatusCode(StatusCodes.Status201Created, PayoutResponse.FromEntity(payout));
    }

    [HttpGet(ApiRoutes.Root + "/payouts/{id:guid}")]
    [EndpointSummary("Get a payout by id")]
    [ProducesResponseType<PayoutResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PayoutResponse>> GetById(
        Guid id, [FromServices] RondiTrackDbContext context, CancellationToken ct)
    {
        var payout = await context.Payouts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("payout.not_found", $"No payout with id {id} exists.");

        // 6.1: this route has no {stokvelId}, so authorize against the payout's OWN stokvel.
        // A non-member gets the SAME payout.not_found as a payout that does not exist.
        await this.EnsureStokvelAccessAsync(payout.StokvelId, Policies.StokvelMember,
            "payout.not_found", $"No payout with id {id} exists.");

        Response.Headers.ETag = $"\"{payout.Version}\"";
        return Ok(PayoutResponse.FromEntity(payout));
    }

    [HttpPut(ApiRoutes.Root + "/payouts/{id:guid}")]
    [EndpointSummary("Update a payout's amount (optimistic concurrency)")]
    [ProducesResponseType<PayoutResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PayoutResponse>> UpdateAmount(
        Guid id,
        [FromBody] decimal amount,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromServices] RondiTrackDbContext context,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ifMatch) || !uint.TryParse(ifMatch.Trim('"'), out var clientVersion))
            throw new ValidationFailedException("payout.if_match_required",
                "A valid If-Match header (the ETag from a previous GET) is required.");

        var payout = await context.Payouts.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("payout.not_found", $"No payout with id {id} exists.");

        // 6.1: changing a payout needs treasurer or chairperson in the payout's stokvel.
        await this.EnsureStokvelAccessAsync(payout.StokvelId, Policies.StokvelTreasurer,
            "payout.not_found", $"No payout with id {id} exists.");

        // Only update if the row's xmin still equals what the client last saw.
        context.Entry(payout).Property(p => p.Version).OriginalValue = clientVersion;

        // Demo-only: Payout has no public update method.
        typeof(Payout).GetProperty(nameof(Payout.Amount))!.SetValue(payout, amount);

        await context.SaveChangesAsync(ct);   // stale token -> DbUpdateConcurrencyException -> central handler -> 409

        Response.Headers.ETag = $"\"{payout.Version}\"";
        return Ok(PayoutResponse.FromEntity(payout));
    }
}