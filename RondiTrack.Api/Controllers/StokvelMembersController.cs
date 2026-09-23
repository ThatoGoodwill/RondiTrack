using Microsoft.AspNetCore.Mvc;
using RondiTrack.Api.Contracts;
using RondiTrack.Api.Data;
using RondiTrack.Api.Services;
namespace RondiTrack.Api.Controllers;
[ApiController]
[Route("api/stokvels/{stokvelId:guid}/members")]
public class StokvelMembersController(IStokvelRepository stokvels, IMembershipService membershipService)
    : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MembershipResponse>>> GetAll(Guid stokvelId, CancellationToken ct)
    {
        var stokvel = await stokvels.GetByIdAsync(stokvelId, ct);
        if (stokvel is null) return NotFoundProblem("stokvel.not_found", $"No stokvel with id {stokvelId} exists.");
        return Ok(stokvel.Members.Select(MembershipResponse.FromEntity).ToList());
    }
    [HttpGet("{userId:guid}")]
    public async Task<ActionResult<MembershipResponse>> GetById(Guid stokvelId, Guid userId, CancellationToken ct)
    {
        var stokvel = await stokvels.GetByIdAsync(stokvelId, ct);
        if (stokvel is null) return NotFoundProblem("stokvel.not_found", $"No stokvel with id {stokvelId} exists.");
        var membership = stokvel.GetMember(userId);
        return membership is null
            ? NotFoundProblem("membership.not_found", "This user is not a member of this stokvel.")
            : Ok(MembershipResponse.FromEntity(membership));
    }
    [HttpPost]
    public async Task<ActionResult<MembershipResponse>> Add(Guid stokvelId, AddMemberRequest request, CancellationToken ct)
    {
        var result = await membershipService.AddMemberAsync(stokvelId, request.UserId, ct);
        if (!result.IsSuccess) return ToProblem(result.Error);
        var response = MembershipResponse.FromEntity(result.Value);
        return CreatedAtAction(nameof(GetById), new { stokvelId, userId = response.UserId }, response);
    }
    [HttpDelete("{userId:guid}")]
    public async Task<IActionResult> Remove(Guid stokvelId, Guid userId, CancellationToken ct)
    {
        var result = await membershipService.RemoveMemberAsync(stokvelId, userId, ct);
        return !result.IsSuccess ? ToProblem(result.Error) : NoContent();
    }
}