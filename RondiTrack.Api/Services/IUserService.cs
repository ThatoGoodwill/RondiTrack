using RondiTrack.Api.Domain;
namespace RondiTrack.Api.Services;
public interface IUserService
{
    Task<Result<User>> CreateUserAsync(string? firstName, string? lastName, string? email,
                                       DateOnly dateOfBirth, CancellationToken ct = default);
    Task<Result<User>> UpdateUserAsync(Guid id, string? firstName, string? lastName, string? email,
                                       DateOnly dateOfBirth, CancellationToken ct = default);
    Task<Result> DeleteUserAsync(Guid id, CancellationToken ct = default);
}