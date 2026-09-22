using Microsoft.AspNetCore.Mvc;
using RondiTrack.Api.Data;
using RondiTrack.Api.Domain;
namespace RondiTrack.Api.Controllers;

[ApiController]
[Route("api/stokvels/{stokvelId:guid}/members")]
public class StokvelMembersController(IStokvelRepository stokvels, IUserRepository users) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Membership>>> GetAll(Guid stokvelId, CancellationToken ct)
    {
        var stokvel = await stokvels.GetByIdAsync(stokvelId, ct);
        return stokvel is null ? NotFound() : Ok(stokvel.Members);
    }

    [HttpGet("{userId:guid}")]
    public async Task<ActionResult<Membership>> GetById(Guid stokvelId, Guid userId, CancellationToken ct)
    {
        var stokvel = await stokvels.GetByIdAsync(stokvelId, ct);
        if (stokvel is null) return NotFound();
        var membership = stokvel.GetMember(userId);
        return membership is null ? NotFound() : Ok(membership);
    }

    [HttpPost]
    public async Task<ActionResult<Membership>> Add(Guid stokvelId, AddMemberRequest request, CancellationToken ct)
    {
        var stokvel = await stokvels.GetByIdAsync(stokvelId, ct);
        if (stokvel is null) return NotFound();

        if (!await users.ExistsAsync(request.UserId, ct))
            return Problem(title: "membership.user_not_found", detail: "No such user.", statusCode: 422);

        var result = stokvel.AddMember(request.UserId);
        if (!result.IsSuccess) return ToProblem(result.Error);

        var membership = stokvel.GetMember(request.UserId)!;
        return CreatedAtAction(nameof(GetById), new { stokvelId, userId = request.UserId }, membership);
    }

    [HttpDelete("{userId:guid}")]
    public async Task<IActionResult> Remove(Guid stokvelId, Guid userId, CancellationToken ct)
    {
        var stokvel = await stokvels.GetByIdAsync(stokvelId, ct);
        if (stokvel is null) return NotFound();
        var result = stokvel.RemoveMember(userId);
        return !result.IsSuccess ? ToProblem(result.Error) : NoContent();
    }
}

public sealed record AddMemberRequest(Guid UserId);