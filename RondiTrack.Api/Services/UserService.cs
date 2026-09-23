using RondiTrack.Api.Data;
using RondiTrack.Api.Domain;
namespace RondiTrack.Api.Services;
public sealed class UserService(IUserRepository users, IStokvelRepository stokvels) : IUserService
{
    private static readonly Error StillAMember = new(ErrorType.Conflict, "user.is_member",
        "This user is a member of at least one stokvel. Remove them from their stokvels first.");
    public async Task<Result<User>> CreateUserAsync(string? firstName, string? lastName, string? email,
                                                     DateOnly dateOfBirth, CancellationToken ct = default)
    {
        // Rule 1: does the entity itself accept these values? (belongs to the entity)
        var result = User.Create(firstName, lastName, email, dateOfBirth);
        if (!result.IsSuccess) return result;
        // Rule 2: is this email already used by someone else? (needs ALL users -> belongs here, not on the entity)
        if (await users.EmailExistsAsync(result.Value.Email, null, ct))
            return Result<User>.Failure(User.EmailTaken);
        await users.AddAsync(result.Value, ct);
        return result;
    }
    public async Task<Result<User>> UpdateUserAsync(Guid id, string? firstName, string? lastName, string? email,
                                                     DateOnly dateOfBirth, CancellationToken ct = default)
    {
        var user = await users.GetByIdAsync(id, ct);
        if (user is null)
            return Result<User>.Failure(new Error(ErrorType.NotFound, "user.not_found", $"No user with id {id} exists."));
        var normalizedEmail = User.NormalizeEmail(email);
        if (normalizedEmail is not null && await users.EmailExistsAsync(normalizedEmail, id, ct))
            return Result<User>.Failure(User.EmailTaken);
        var result = user.Update(firstName, lastName, email, dateOfBirth);
        return !result.IsSuccess ? Result<User>.Failure(result.Error) : Result<User>.Success(user);
    }


    public async Task<Result> DeleteUserAsync(Guid id, CancellationToken ct = default)
    {
        if (!await users.ExistsAsync(id, ct))
            return Result.Failure(new Error(ErrorType.NotFound, "user.not_found", $"No user with id {id} exists."));
        // Cross-entity rule: needs to check EVERY stokvel -> belongs here, not on User or Stokvel alone.
        if (await stokvels.HasMemberAsync(id, ct))
            return Result.Failure(StillAMember);
        await users.DeleteAsync(id, ct);
        return Result.Success();
    }
}