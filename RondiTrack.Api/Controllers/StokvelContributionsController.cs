using Microsoft.AspNetCore.Mvc;
using RondiTrack.Api.Contracts;
using RondiTrack.Api.Data;
using RondiTrack.Api.Services;
namespace RondiTrack.Api.Controllers;
[ApiController]
[Route("api/stokvels/{stokvelId:guid}/contributions")]
public class StokvelContributionsController(
    IContributionRepository contributions, IContributionService contributionService) : ApiControllerBase
{
    // GET is pure read, no decision -> talks straight to the repository, same reasoning as StokvelsController.
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ContributionResponse>>> GetAll(Guid stokvelId, CancellationToken ct)
    {
        var list = await contributions.GetByStokvelAsync(stokvelId, ct);
        return Ok(list.Select(ContributionResponse.FromEntity).ToList());
    }
    // POST is the money-moving, idempotent operation -> goes through the service.
    [HttpPost]
    public async Task<ActionResult<ContributionResponse>> Record(
        Guid stokvelId,
        ContributionRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
        var result = await contributionService.RecordContributionAsync(stokvelId, request, idempotencyKey, ct);
        if (!result.IsSuccess) return ToProblem(result.Error);
        // Whether this was a fresh recording OR a replay of an identical earlier request,
        // we return the SAME shape: 201 with the contribution. That's the whole idempotency guarantee,
        // visible from the outside as "you cannot tell a replay apart from the original response."
        return StatusCode(StatusCodes.Status201Created, result.Value.Response);
    }
}