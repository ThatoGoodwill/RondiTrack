using FluentValidation;
using RondiTrack.Api.Contracts;

namespace RondiTrack.Api.Validators;

// SHAPE-ONLY rules. "Is this email taken?" needs the repository, so it is a 409 from the service.
// The name / age rules stay on the User entity, exactly as for POST /api/users.
public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public const int MinPasswordLength = 12;
    public const int MaxPasswordLength = 128;   // stops someone sending a 10 MB "password" to be hashed

    public RegisterRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().MaximumLength(254).EmailAddress();
        RuleFor(x => x.DateOfBirth)
            .Must(d => d < DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Date of birth must be in the past.");
        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(MinPasswordLength).WithMessage($"Password must be at least {MinPasswordLength} characters.")
            .MaximumLength(MaxPasswordLength).WithMessage($"Password cannot exceed {MaxPasswordLength} characters.");
    }
}

// Login only checks that BOTH fields were sent. It must NOT apply the registration rules
// (length, email format): a different 400 for "too short" would tell an attacker something.
public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(254);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(RegisterRequestValidator.MaxPasswordLength);
    }
}
