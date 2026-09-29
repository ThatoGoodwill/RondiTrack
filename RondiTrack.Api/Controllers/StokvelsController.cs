using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RondiTrack.Api.Contracts;
using RondiTrack.Api.Data;
using RondiTrack.Api.Domain;
using RondiTrack.Api.Exceptions;

namespace RondiTrack.Api.Controllers;

// Plain CRUD talking straight to the repository: NO service on purpose.
// Nothing here needs a decision beyond ONE stokvel's own fields.
[ApiController]
[Route("api/stokvels")]
public class StokvelsController(IStokvelRepository stokvels) : ApiControllerBase
{
    [HttpGet]
    [EndpointSummary("List all stokvels")]
    [EndpointDescription("Returns every stokvel, with its member COUNT (not the full member list). " +
                          "Use GET /api/stokvels/{id}/members for the full list.")]
    [ProducesResponseType<List<StokvelResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<StokvelResponse>>> GetAll(CancellationToken ct)
    {
        var all = await stokvels.GetAllAsync(ct);
        return Ok(all.Select(StokvelResponse.FromEntity).ToList());
    }

    [HttpGet("{id:guid}")]
    [EndpointSummary("Get a stokvel by id")]
    [EndpointDescription("Returns a single stokvel. 404 if no stokvel with that id exists.")]
    [ProducesResponseType<StokvelResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StokvelResponse>> GetById(Guid id, CancellationToken ct)
    {
        var stokvel = await stokvels.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("stokvel.not_found", $"No stokvel with id {id} exists.");
        return Ok(StokvelResponse.FromEntity(stokvel));
    }

    [HttpPost]
    [EndpointSummary("Create a new stokvel")]
    [EndpointDescription(
        "Creates a new stokvel. Contribution amount must be positive with at most 2 decimal " +
        "places. Max members must be between 2 and 50.")]
    [ProducesResponseType<StokvelResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StokvelResponse>> Create(StokvelRequest request, CancellationToken ct)
    {
        var stokvel = Stokvel.Create(request.Name, request.ContributionAmount, request.Frequency, request.MaxMembers)
            .ThrowIfFailure();

        await stokvels.AddAsync(stokvel, ct);
        var response = StokvelResponse.FromEntity(stokvel);
        return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
    }

    [HttpPut("{id:guid}")]
    [EndpointSummary("Replace a stokvel's details")]
    [EndpointDescription(
        "Fully replaces a stokvel's name, contribution amount, frequency and max members. " +
        "Fails with 409 if the new max members would be lower than the current member count.")]
    [ProducesResponseType<StokvelResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StokvelResponse>> Replace(Guid id, StokvelRequest request, CancellationToken ct)
    {
        var stokvel = await stokvels.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("stokvel.not_found", $"No stokvel with id {id} exists.");

        stokvel.Update(request.Name, request.ContributionAmount, request.Frequency, request.MaxMembers)
            .ThrowIfFailure();

        return Ok(StokvelResponse.FromEntity(stokvel));
    }

    [HttpDelete("{id:guid}")]
    [EndpointSummary("Delete a stokvel")]
    [EndpointDescription("Deletes a stokvel and all of its memberships.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (!await stokvels.DeleteAsync(id, ct))
            throw new NotFoundException("stokvel.not_found", $"No stokvel with id {id} exists.");
        return NoContent();
    }
}
