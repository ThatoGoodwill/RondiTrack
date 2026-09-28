using FluentValidation;
using RondiTrack.Api.Contracts;

namespace RondiTrack.Api.Validators;

// SHAPE-ONLY rules for UserRequest: "is this form filled in sensibly?"
// A validator NEVER touches a repository. "Is this email already taken?" needs the
// repository, so that is NOT here: it is a ConflictException thrown by the service.
// (The 18+ age rule stays on the User entity so the age arithmetic exists in one place.)
public sealed class UserRequestValidator : AbstractValidator<UserRequest>
{
    public UserRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().MaximumLength(254).EmailAddress();
        RuleFor(x => x.DateOfBirth)
            .Must(d => d < DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Date of birth must be in the past.");
    }
}
