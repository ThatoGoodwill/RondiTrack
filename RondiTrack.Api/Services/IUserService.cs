using RondiTrack.Api.Domain;

namespace RondiTrack.Api.Services;

// The DECISIONS about users that need to look beyond one User object.
// Methods return Result; the controller converts a failure into an exception.
public interface IUserService
{
    Task<Result<User>> CreateUserAsync(string? firstName, string? lastName, string? email,
                                       DateOnly dateOfBirth, CancellationToken ct = default);

    Task<Result<User>> UpdateUserAsync(Guid id, string? firstName, string? lastName, string? email,
                                       DateOnly dateOfBirth, CancellationToken ct = default);

    Task<Result> DeleteUserAsync(Guid id, CancellationToken ct = default);
}
