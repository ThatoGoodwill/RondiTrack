using Microsoft.AspNetCore.Mvc;
using RondiTrack.Api.Contracts;
using RondiTrack.Api.Data;
using RondiTrack.Api.Domain;       // for the ThrowIfFailure() extension
using RondiTrack.Api.Exceptions;
using RondiTrack.Api.Services;

namespace RondiTrack.Api.Controllers;

// ---------------------------------------------------------------------------------
// HTTP only. This controller decides NOTHING and formats NO errors:
//   - request shape  -> checked by ValidationFilter BEFORE these methods run
//   - business rules -> decided by UserService, arriving here as a Result
//   - failures       -> THROWN (ThrowIfFailure / throw new ...), answered by the handler
// (No "User" type is referenced here, which also avoids the ControllerBase.User clash.)
// ---------------------------------------------------------------------------------
[ApiController]
[Route("api/users")]
public class UsersController(IUserRepository users, IUserService userService) : ApiControllerBase
{
    // GET /api/users            -> 200
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserResponse>>> GetAll(CancellationToken ct)
    {
        var all = await users.GetAllAsync(ct);
        return Ok(all.Select(UserResponse.FromEntity).ToList());
    }

    // GET /api/users/{id}       -> 200, or 404 (thrown)
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserResponse>> GetById(Guid id, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("user.not_found", $"No user with id {id} exists.");
        return Ok(UserResponse.FromEntity(user));
    }

    // POST /api/users           -> 201, or 400 / 409 (thrown)
    [HttpPost]
    public async Task<ActionResult<UserResponse>> Create(UserRequest request, CancellationToken ct)
    {
        var user = (await userService.CreateUserAsync(
            request.FirstName, request.LastName, request.Email, request.DateOfBirth, ct)).ThrowIfFailure();

        var response = UserResponse.FromEntity(user);
        return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
    }

    // PUT /api/users/{id}       -> 200, or 400 / 404 / 409 (thrown)
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UserResponse>> Replace(Guid id, UserRequest request, CancellationToken ct)
    {
        var user = (await userService.UpdateUserAsync(
            id, request.FirstName, request.LastName, request.Email, request.DateOfBirth, ct)).ThrowIfFailure();

        return Ok(UserResponse.FromEntity(user));
    }

    // DELETE /api/users/{id}    -> 204, or 404 / 409 (thrown)
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        (await userService.DeleteUserAsync(id, ct)).ThrowIfFailure();
        return NoContent();
    }
}
