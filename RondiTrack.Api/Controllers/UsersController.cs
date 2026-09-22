using Microsoft.AspNetCore.Mvc;
using RondiTrack.Api.Data;
using RondiTrack.Api.Domain;
using DomainUser = RondiTrack.Api.Domain.User;   // avoids clash with ControllerBase.User

namespace RondiTrack.Api.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController(IUserRepository users, IStokvelRepository stokvels) : ApiControllerBase
{
    private static readonly Error StillAMember = new(ErrorType.Conflict, "user.is_member",
        "This user is a member of a stokvel. Remove them first.");

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DomainUser>>> GetAll(CancellationToken ct) =>
        Ok(await users.GetAllAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DomainUser>> GetById(Guid id, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(id, ct);
        return user is null ? NotFound() : Ok(user);
    }

    [HttpPost]
    public async Task<ActionResult<DomainUser>> Create(UserRequest request, CancellationToken ct)
    {
        var result = DomainUser.Create(request.FirstName, request.LastName, request.Email, request.DateOfBirth);
        if (!result.IsSuccess) return ToProblem(result.Error);

        var user = result.Value;
        if (await users.EmailExistsAsync(user.Email, null, ct)) return ToProblem(DomainUser.EmailTaken);

        await users.AddAsync(user, ct);
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, user);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DomainUser>> Replace(Guid id, UserRequest request, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(id, ct);
        if (user is null) return NotFound();

        var email = DomainUser.NormalizeEmail(request.Email);
        if (email is not null && await users.EmailExistsAsync(email, id, ct)) return ToProblem(DomainUser.EmailTaken);

        var result = user.Update(request.FirstName, request.LastName, request.Email, request.DateOfBirth);
        return !result.IsSuccess ? ToProblem(result.Error) : Ok(user);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (!await users.ExistsAsync(id, ct)) return NotFound();
        if (await stokvels.HasMemberAsync(id, ct)) return ToProblem(StillAMember);
        await users.DeleteAsync(id, ct);
        return NoContent();
    }
}

public sealed record UserRequest(string? FirstName, string? LastName, string? Email, DateOnly DateOfBirth);