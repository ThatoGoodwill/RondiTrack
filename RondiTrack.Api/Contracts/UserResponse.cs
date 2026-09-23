using RondiTrack.Api.Domain;
namespace RondiTrack.Api.Contracts;
// The ONE shape a User takes on the wire. FromEntity is the ONE place that decides it.
public sealed record UserResponse(Guid Id, string FirstName, string LastName, string Email, DateOnly DateOfBirth)
{
    public static UserResponse FromEntity(User user) =>
        new(user.Id, user.FirstName, user.LastName, user.Email, user.DateOfBirth);
}