using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RondiTrack.Api.Contracts;
using RondiTrack.Api.Domain;
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
}