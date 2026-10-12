using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RondiTrack.Api.Auth;
using RondiTrack.Api.Authorization;
using RondiTrack.Api.Contracts;
using RondiTrack.Domain;
using RondiTrack.Domain.Exceptions;
using RondiTrack.Domain.Repositories;

namespace RondiTrack.Api.Controllers;

// Plain CRUD talking straight to the repository: NO service on purpose.
// Nothing here needs a decision beyond ONE stokvel's own fields.
[ApiController]
[Route("api/stokvels")]
public class StokvelsController(IStokvelRepository stokvels) : ApiControllerBase
{
    // THE one plain role check in the app (6.1): listing EVERY stokvel is a platform-level operation.
    // Ordinary users never need it: they only ever see stokvels they belong to.
    [HttpGet]
    [Authorize(Roles = PlatformRoles.PlatformAdmin)]
    [EndpointSummary("List all stokvels (platform admins only)")]
    [EndpointDescription("Returns every stokvel, with its member COUNT (not the full member list). " +
                          "Use GET /api/stokvels/{id}/members for the full list.")]
    [ProducesResponseType<List<StokvelResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<StokvelResponse>>> GetAll(CancellationToken ct)
    {
        var all = await stokvels.GetAllAsync(ct);
        return Ok(all.Select(StokvelResponse.FromEntity).ToList());
    }

    [HttpGet("{stokvelId:guid}")]
    [EndpointSummary("Get a stokvel by id")]
    [EndpointDescription("Returns a single stokvel. 404 if no stokvel with that id exists.")]
    [ProducesResponseType<StokvelResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StokvelResponse>> GetById(Guid stokvelId, CancellationToken ct)
    {
        var stokvel = await stokvels.GetByIdAsync(stokvelId, ct)
            ?? throw new NotFoundException("stokvel.not_found", $"No stokvel with id {stokvelId} exists.");
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

        // 6.1: whoever creates a stokvel becomes its chairperson (Admin), otherwise nobody could use it.
        // Platform admins create stokvels on behalf of others and are not added as members.
        if (!User.IsPlatformAdmin() && User.GetUserId() is { } creatorId)
        {
            stokvel.AddMember(creatorId).ThrowIfFailure();
            stokvel.GetMember(creatorId)!.ChangeRole(StokvelMemberRole.Admin);
        }

        await stokvels.AddAsync(stokvel, ct);
        var response = StokvelResponse.FromEntity(stokvel);
        return CreatedAtAction(nameof(GetById), new { stokvelId = response.Id }, response);
    }

    [HttpPut("{stokvelId:guid}")]
    [StokvelAccess(Policies.StokvelChairperson)]
    [EndpointSummary("Replace a stokvel's details")]
    [EndpointDescription(
        "Fully replaces a stokvel's name, contribution amount, frequency and max members. " +
        "Fails with 409 if the new max members would be lower than the current member count.")]
    [ProducesResponseType<StokvelResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<StokvelResponse>> Replace(Guid stokvelId, StokvelRequest request, CancellationToken ct)
    {
        var stokvel = await stokvels.GetByIdAsync(stokvelId, ct)
            ?? throw new NotFoundException("stokvel.not_found", $"No stokvel with id {stokvelId} exists.");

        stokvel.Update(request.Name, request.ContributionAmount, request.Frequency, request.MaxMembers)
            .ThrowIfFailure();

        await stokvels.SaveChangesAsync(ct);   // 6.1: stokvels live in PostgreSQL now

        return Ok(StokvelResponse.FromEntity(stokvel));
    }

    [HttpDelete("{stokvelId:guid}")]
    [StokvelAccess(Policies.StokvelChairperson)]
    [EndpointSummary("Delete a stokvel")]
    [EndpointDescription("Deletes a stokvel and all of its memberships.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid stokvelId, CancellationToken ct)
    {
        if (!await stokvels.DeleteAsync(stokvelId, ct))
            throw new NotFoundException("stokvel.not_found", $"No stokvel with id {stokvelId} exists.");
        return NoContent();
    }
}
