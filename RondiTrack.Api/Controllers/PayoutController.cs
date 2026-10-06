using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RondiTrack.Api.Contracts;
using RondiTrack.Api.Data;
using RondiTrack.Api.Domain;
using RondiTrack.Api.Exceptions;
using RondiTrack.Api.Services;

namespace RondiTrack.Api.Controllers;

[ApiController]
[Route("api/stokvels/{stokvelId:guid}/cycles/{cycleId:guid}/payout")]
public class PayoutsController(IPayoutService payoutService) : ApiControllerBase
{
    [HttpPost]
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

    [HttpGet("/api/payouts/{id:guid}")]
    [EndpointSummary("Get a payout by id")]
    [ProducesResponseType<PayoutResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PayoutResponse>> GetById(
        Guid id, [FromServices] RondiTrackDbContext context, CancellationToken ct)
    {
        var payout = await context.Payouts.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new NotFoundException("payout.not_found", $"No payout with id {id} exists.");

        Response.Headers.ETag = $"\"{payout.Version}\"";
        return Ok(PayoutResponse.FromEntity(payout));
    }

    [HttpPut("/api/payouts/{id:guid}")]
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

        // Only update if the row's xmin still equals what the client last saw.
        context.Entry(payout).Property(p => p.Version).OriginalValue = clientVersion;

        // Demo-only: Payout has no public update method.
        typeof(Payout).GetProperty(nameof(Payout.Amount))!.SetValue(payout, amount);

        await context.SaveChangesAsync(ct);   // stale token -> DbUpdateConcurrencyException -> central handler -> 409

        Response.Headers.ETag = $"\"{payout.Version}\"";
        return Ok(PayoutResponse.FromEntity(payout));
    }
}