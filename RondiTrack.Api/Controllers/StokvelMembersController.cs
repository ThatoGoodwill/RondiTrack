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
    // GET /api/stokvels/{stokvelId}/members  -> 200, or 404 (stokvel missing)
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MembershipResponse>>> GetAll(Guid stokvelId, CancellationToken ct)
    {
        var stokvel = await stokvels.GetByIdAsync(stokvelId, ct)
            ?? throw new NotFoundException("stokvel.not_found", $"No stokvel with id {stokvelId} exists.");
        return Ok(stokvel.Members.Select(MembershipResponse.FromEntity).ToList());
    }

    // GET /api/stokvels/{stokvelId}/members/{userId}  -> 200, or 404
    [HttpGet("{userId:guid}")]
    public async Task<ActionResult<MembershipResponse>> GetById(Guid stokvelId, Guid userId, CancellationToken ct)
    {
        var stokvel = await stokvels.GetByIdAsync(stokvelId, ct)          // check the PARENT first
            ?? throw new NotFoundException("stokvel.not_found", $"No stokvel with id {stokvelId} exists.");

        var membership = stokvel.GetMember(userId)
            ?? throw new NotFoundException("membership.not_found", "This user is not a member of this stokvel.");
        return Ok(MembershipResponse.FromEntity(membership));
    }

    // POST /api/stokvels/{stokvelId}/members  -> 201, or 400 / 404 / 409 / 422
    [HttpPost]
    public async Task<ActionResult<MembershipResponse>> Add(Guid stokvelId, AddMemberRequest request, CancellationToken ct)
    {
        var membership = (await membershipService.AddMemberAsync(stokvelId, request.UserId, ct)).ThrowIfFailure();

        var response = MembershipResponse.FromEntity(membership);
        return CreatedAtAction(nameof(GetById), new { stokvelId, userId = response.UserId }, response);
    }

    // DELETE /api/stokvels/{stokvelId}/members/{userId}  -> 204, or 404
    [HttpDelete("{userId:guid}")]
    public async Task<IActionResult> Remove(Guid stokvelId, Guid userId, CancellationToken ct)
    {
        (await membershipService.RemoveMemberAsync(stokvelId, userId, ct)).ThrowIfFailure();
        return NoContent();
    }
}
