using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RondiTrack.Api.Contracts;
using RondiTrack.Api.Data;
using RondiTrack.Api.Domain;
using RondiTrack.Api.Exceptions;
using RondiTrack.Api.Services;

namespace RondiTrack.Api.Controllers;

// ---------------------------------------------------------------------------------
// HTTP only. Decides NOTHING, formats NO errors:
//   - request shape  -> checked by ValidationFilter BEFORE these methods run
//   - business rules -> decided by UserService, arriving here as a Result
//   - failures       -> THROWN (ThrowIfFailure / throw new ...), answered by the handler
// ---------------------------------------------------------------------------------
[ApiController]
[Route("api/users")]
public class UsersController(IUserRepository users, IUserService userService) : ApiControllerBase
{
    [HttpGet]
    [EndpointSummary("List all users")]
    [EndpointDescription("Returns every registered user, ordered by last name then first name.")]
    [ProducesResponseType<List<UserResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<UserResponse>>> GetAll(CancellationToken ct)
    {
        var all = await users.GetAllAsync(ct);
        return Ok(all.Select(UserResponse.FromEntity).ToList());
    }

    [HttpGet("{id:guid}")]
    [EndpointSummary("Get a user by id")]
    [EndpointDescription("Returns a single user. 404 if no user with that id exists.")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponse>> GetById(Guid id, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(id, ct)
            ?? throw new NotFoundException("user.not_found", $"No user with id {id} exists.");
        return Ok(UserResponse.FromEntity(user));
    }

    [HttpPost]
    [EndpointSummary("Register a new user")]
    [EndpointDescription(
        "Creates a new user. Requires a non-blank first/last name, a valid-looking unique " +
        "email address, and a date of birth showing the person is at least 18 years old.")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserResponse>> Create(UserRequest request, CancellationToken ct)
    {
        var user = (await userService.CreateUserAsync(
            request.FirstName, request.LastName, request.Email, request.DateOfBirth, ct)).ThrowIfFailure();

        var response = UserResponse.FromEntity(user);
        return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
    }

    [HttpPut("{id:guid}")]
    [EndpointSummary("Replace a user's details")]
    [EndpointDescription("Fully replaces a user's first name, last name, email and date of birth.")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserResponse>> Replace(Guid id, UserRequest request, CancellationToken ct)
    {
        var user = (await userService.UpdateUserAsync(
            id, request.FirstName, request.LastName, request.Email, request.DateOfBirth, ct)).ThrowIfFailure();

        return Ok(UserResponse.FromEntity(user));
    }

    [HttpDelete("{id:guid}")]
    [EndpointSummary("Delete a user")]
    [EndpointDescription("Deletes a user. Fails with 409 if the user is still a member of any stokvel.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        (await userService.DeleteUserAsync(id, ct)).ThrowIfFailure();
        return NoContent();
    }
}
