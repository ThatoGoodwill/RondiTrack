using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RondiTrack.Api.Contracts;
using RondiTrack.Api.Data;
using RondiTrack.Api.Domain;
using RondiTrack.Api.Exceptions;

namespace RondiTrack.Api.Controllers;

// Plain CRUD, NO SERVICE, on purpose: creating a cycle needs exactly one existence check
// (does the stokvel exist?), a single repository lookup with no further decision attached.
[ApiController]
[Route("api/stokvels/{stokvelId:guid}/cycles")]
public class ContributionCyclesController(
    IStokvelRepository stokvels, IContributionCycleRepository cycles) : ApiControllerBase
{
    [HttpGet]
    [EndpointSummary("List a stokvel's contribution cycles")]
    [EndpointDescription("Returns every contribution cycle defined for this stokvel.")]
    [ProducesResponseType<List<ContributionCycleResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ContributionCycleResponse>>> GetAll(Guid stokvelId, CancellationToken ct)
    {
        await EnsureStokvelExists(stokvelId, ct);
        var list = await cycles.GetByStokvelAsync(stokvelId, ct);
        return Ok(list.Select(ContributionCycleResponse.FromEntity).ToList());
    }

    [HttpGet("{id:guid}")]
    [EndpointSummary("Get one contribution cycle")]
    [EndpointDescription("Returns one contribution cycle. 404 if it does not exist for this stokvel.")]
    [ProducesResponseType<ContributionCycleResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContributionCycleResponse>> GetById(Guid stokvelId, Guid id, CancellationToken ct)
    {
        var cycle = await GetCycleOrThrow(stokvelId, id, ct);
        return Ok(ContributionCycleResponse.FromEntity(cycle));
    }

    [HttpPost]
    [EndpointSummary("Create a contribution cycle")]
    [EndpointDescription("Defines a new collection period for this stokvel, e.g. \"2026-09\" with a target amount.")]
    [ProducesResponseType<ContributionCycleResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContributionCycleResponse>> Create(
        Guid stokvelId, ContributionCycleRequest request, CancellationToken ct)
    {
        await EnsureStokvelExists(stokvelId, ct);

        var cycle = ContributionCycle.Create(stokvelId, request.Label, request.TargetAmount).ThrowIfFailure();
        await cycles.AddAsync(cycle, ct);

        var response = ContributionCycleResponse.FromEntity(cycle);
        return CreatedAtAction(nameof(GetById), new { stokvelId, id = response.Id }, response);
    }

    [HttpPut("{id:guid}")]
    [EndpointSummary("Replace a contribution cycle")]
    [EndpointDescription("Replaces a cycle's label and target amount.")]
    [ProducesResponseType<ContributionCycleResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContributionCycleResponse>> Replace(
        Guid stokvelId, Guid id, ContributionCycleRequest request, CancellationToken ct)
    {
        var cycle = await GetCycleOrThrow(stokvelId, id, ct);
        cycle.Update(request.Label, request.TargetAmount).ThrowIfFailure();
        return Ok(ContributionCycleResponse.FromEntity(cycle));
    }

    [HttpDelete("{id:guid}")]
    [EndpointSummary("Delete a contribution cycle")]
    [EndpointDescription("Deletes a contribution cycle.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid stokvelId, Guid id, CancellationToken ct)
    {
        await GetCycleOrThrow(stokvelId, id, ct);
        await cycles.DeleteAsync(id, ct);
        return NoContent();
    }

    private async Task EnsureStokvelExists(Guid stokvelId, CancellationToken ct)
    {
        _ = await stokvels.GetByIdAsync(stokvelId, ct)
            ?? throw new NotFoundException("stokvel.not_found", $"No stokvel with id {stokvelId} exists.");
    }

    private async Task<ContributionCycle> GetCycleOrThrow(Guid stokvelId, Guid id, CancellationToken ct)
    {
        await EnsureStokvelExists(stokvelId, ct);
        var cycle = await cycles.GetByIdAsync(id, ct);
        if (cycle is null || cycle.StokvelId != stokvelId)
            throw new NotFoundException("cycle.not_found", $"No contribution cycle with id {id} exists for this stokvel.");
        return cycle;
    }
}
