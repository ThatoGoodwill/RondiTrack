using RondiTrack.Api.Domain;

namespace RondiTrack.Api.Contracts;

// OUTGOING shape of a user. FromEntity is the ONE place that decides what a User
// looks like once it leaves the server, so a new internal field on User can never leak by accident.
public sealed record UserResponse(Guid Id, string FirstName, string LastName, string Email, DateOnly DateOfBirth)
{
    public static UserResponse FromEntity(User user) =>
        new(user.Id, user.FirstName, user.LastName, user.Email, user.DateOfBirth);
}
