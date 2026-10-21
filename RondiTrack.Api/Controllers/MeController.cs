using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RondiTrack.Api.Auth;
using RondiTrack.Api.Contracts;
using RondiTrack.Api.Services;
using RondiTrack.Domain.Exceptions;
using RondiTrack.Domain.Repositories;

namespace RondiTrack.Api.Controllers;

[ApiController]
[Route(ApiRoutes.Base + "/me")]
public class MeController(IHomeService home, IUserRepository users) : ApiControllerBase
{
    [HttpGet]
    [EndpointSummary("Get the current authenticated user")]
    [EndpointDescription("Returns the basic profile (id, name, email) of the signed-in member.")]
    [ProducesResponseType<MeResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<MeResponse>> GetMe(CancellationToken ct)
    {
        var userId = User.GetUserId()
            ?? throw new AuthenticationFailedException("auth.unauthenticated", "Authentication is required to access this resource.");
            
        var user = await users.GetByIdAsync(userId, ct);
        if (user is null) return NotFound();

        return Ok(new MeResponse(user.Id, user.FirstName, user.LastName, user.Email, user.DateOfBirth));
    }

    [HttpGet("home")]
    [EndpointSummary("The signed-in member's home screen")]
    [EndpointDescription("One response covering every stokvel the caller belongs to: their role, the current cycle, " +
                         "what they paid, what they still owe (integer cents) and the next payout date (UTC). " +
                         "A caller in no stokvels gets an empty list.")]
    [ProducesResponseType<HomeResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<HomeResponse>> GetHome(CancellationToken ct)
    {
        var userId = User.GetUserId()
            ?? throw new AuthenticationFailedException("auth.unauthenticated", "Authentication is required to access this resource.");
        return Ok(await home.GetHomeAsync(userId, ct));
    }
}