using Microsoft.AspNetCore.Mvc;
using RondiTrack.Api.Contracts;
using RondiTrack.Api.Data;
using RondiTrack.Api.Services;
namespace RondiTrack.Api.Controllers;
[ApiController]
[Route("api/users")]
public class UsersController(IUserRepository users, IUserService userService) : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserResponse>>> GetAll(CancellationToken ct)
    {
        var all = await users.GetAllAsync(ct);
        return Ok(all.Select(UserResponse.FromEntity).ToList());
    }
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserResponse>> GetById(Guid id, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(id, ct);
        return user is null
            ? NotFoundProblem("user.not_found", $"No user with id {id} exists.")
            : Ok(UserResponse.FromEntity(user));
    }
    [HttpPost]
    public async Task<ActionResult<UserResponse>> Create(UserRequest request, CancellationToken ct)
    {
        var result = await userService.CreateUserAsync(
            request.FirstName, request.LastName, request.Email, request.DateOfBirth, ct);
        if (!result.IsSuccess) return ToProblem(result.Error);
        var response = UserResponse.FromEntity(result.Value);
        return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
    }
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UserResponse>> Replace(Guid id, UserRequest request, CancellationToken ct)
    {
        var result = await userService.UpdateUserAsync(
            id, request.FirstName, request.LastName, request.Email, request.DateOfBirth, ct);
        return !result.IsSuccess ? ToProblem(result.Error) : Ok(UserResponse.FromEntity(result.Value));
    }
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await userService.DeleteUserAsync(id, ct);
        return !result.IsSuccess ? ToProblem(result.Error) : NoContent();
    }
}
