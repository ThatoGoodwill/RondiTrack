using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RondiTrack.Api.Contracts;
using RondiTrack.Api.Data;
using RondiTrack.Api.Domain;
using RondiTrack.Api.Exceptions;
using RondiTrack.Api.Services;

namespace RondiTrack.Api.Controllers;

// Nested route: a membership only exists INSIDE a stokvel.
// Add/remove go through MembershipService (the decisions); reads talk to the repository.
[ApiController]
[Route("api/stokvels/{stokvelId:guid}/members")]
public class StokvelMembersController(IStokvelRepository stokvels, IMembershipService membershipService)
    : ApiControllerBase
{
    [HttpGet]
    [EndpointSummary("List a stokvel's members")]
    [EndpointDescription("Returns every membership for the given stokvel. Returns an empty list, not an error, if the stokvel has no members yet.")]
    [ProducesResponseType<List<MembershipResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<MembershipResponse>>> GetAll(Guid stokvelId, CancellationToken ct)
    {
        var stokvel = await stokvels.GetByIdAsync(stokvelId, ct)
            ?? throw new NotFoundException("stokvel.not_found", $"No stokvel with id {stokvelId} exists.");
        return Ok(stokvel.Members.Select(MembershipResponse.FromEntity).ToList());
    }

    [HttpGet("{userId:guid}")]
    [EndpointSummary("Get one membership")]
    [EndpointDescription("Returns one membership. 404 if either the stokvel or the membership does not exist.")]
    [ProducesResponseType<MembershipResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MembershipResponse>> GetById(Guid stokvelId, Guid userId, CancellationToken ct)
    {
        var stokvel = await stokvels.GetByIdAsync(stokvelId, ct)
            ?? throw new NotFoundException("stokvel.not_found", $"No stokvel with id {stokvelId} exists.");

        var membership = stokvel.GetMember(userId)
            ?? throw new NotFoundException("membership.not_found", "This user is not a member of this stokvel.");
        return Ok(MembershipResponse.FromEntity(membership));
    }

    [HttpPost]
    [EndpointSummary("Add a member to a stokvel")]
    [EndpointDescription(
        "Adds an existing user as a member of an existing stokvel. Fails if the stokvel or user " +
        "does not exist, if the user is already a member, or if the stokvel has reached its " +
        "maximum member count. Membership order is not guaranteed to reflect payout order.")]
    [ProducesResponseType<MembershipResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<MembershipResponse>> Add(Guid stokvelId, AddMemberRequest request, CancellationToken ct)
    {
        var membership = (await membershipService.AddMemberAsync(stokvelId, request.UserId, ct)).ThrowIfFailure();

        var response = MembershipResponse.FromEntity(membership);
        return CreatedAtAction(nameof(GetById), new { stokvelId, userId = response.UserId }, response);
    }

    [HttpDelete("{userId:guid}")]
    [EndpointSummary("Remove a member from a stokvel")]
    [EndpointDescription("Removes a membership. 404 if the stokvel does not exist or the user is not currently a member.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Remove(Guid stokvelId, Guid userId, CancellationToken ct)
    {
        (await membershipService.RemoveMemberAsync(stokvelId, userId, ct)).ThrowIfFailure();
        return NoContent();
    }
}
