using RondiTrack.Api.Data;
using RondiTrack.Api.Domain;

namespace RondiTrack.Api.Services;

// Owns the user decisions that need to see OTHER data:
//   - is this email already used by someone else?      (needs ALL users)
//   - is this user still a member of any stokvel?      (needs ALL stokvels)
// The User entity still guards its own fields (blank name, 18+, email format).
public sealed class UserService(IUserRepository users, IStokvelRepository stokvels) : IUserService
{
    private static Error EmailTaken() => new(ErrorType.Conflict, "user.email_taken",
        "A user with this email address already exists.");

    public async Task<Result<User>> CreateUserAsync(string? firstName, string? lastName, string? email,
                                                     DateOnly dateOfBirth, CancellationToken ct = default)
    {
        // Rule 1 (entity): are the values themselves acceptable?
        var result = User.Create(firstName, lastName, email, dateOfBirth);
        if (!result.IsSuccess) return result;

        // Rule 2 (service): is the email already taken? Needs the repository.
        if (await users.EmailExistsAsync(result.Value.Email, null, ct))
            return Result<User>.Failure(EmailTaken());

        await users.AddAsync(result.Value, ct);
        return result;
    }

    public async Task<Result<User>> UpdateUserAsync(Guid id, string? firstName, string? lastName, string? email,
                                                     DateOnly dateOfBirth, CancellationToken ct = default)
    {
        var user = await users.GetByIdAsync(id, ct);
        if (user is null)
            return Result<User>.Failure(new Error(ErrorType.NotFound, "user.not_found", $"No user with id {id} exists."));

        // Is ANOTHER user already using this (normalised) email? (excluding this user's own id)
        var normalizedEmail = User.NormalizeEmail(email);
        if (normalizedEmail is not null && await users.EmailExistsAsync(normalizedEmail, id, ct))
            return Result<User>.Failure(EmailTaken());

        var result = user.Update(firstName, lastName, email, dateOfBirth);
       if (!result.IsSuccess) return Result<User>.Failure(result.Error);
await users.AddAsync(user, ct);   // harmless no-op for already-tracked entities against EF Core; triggers SaveChanges
return Result<User>.Success(user);
    }

    public async Task<Result> DeleteUserAsync(Guid id, CancellationToken ct = default)
    {
        if (!await users.ExistsAsync(id, ct))
            return Result.Failure(new Error(ErrorType.NotFound, "user.not_found", $"No user with id {id} exists."));

        // Cross-entity rule: a user who still belongs to a stokvel cannot be deleted.
        if (await stokvels.HasMemberAsync(id, ct))
            return Result.Failure(new Error(ErrorType.Conflict, "user.is_member",
                "This user is a member of at least one stokvel. Remove them from their stokvels first."));

        await users.DeleteAsync(id, ct);
        return Result.Success();
    }
}
