namespace RondiTrack.Api.Domain;

public sealed class User
{
    public const int MaxNameLength = 100;
    public const int MinimumAge = 18;

    public static readonly Error EmailTaken = new(
        ErrorType.Conflict, "user.email_taken", "A user with this email address already exists.");

    private User(Guid id, string firstName, string lastName, string email,
                 DateOnly dateOfBirth, DateTimeOffset createdAt)
    {
        Id = id; FirstName = firstName; LastName = lastName;
        Email = email; DateOfBirth = dateOfBirth; CreatedAt = createdAt;
    }

    public Guid Id { get; }
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public string Email { get; private set; }
    public DateOnly DateOfBirth { get; private set; }
    public DateTimeOffset CreatedAt { get; }

    public static Result<User> Create(string? firstName, string? lastName, string? email, DateOnly dateOfBirth)
    {
        var error = Validate(firstName, lastName, email, dateOfBirth);
        if (error is not null) return Result<User>.Failure(error);
        return Result<User>.Success(new User(Guid.CreateVersion7(), firstName!.Trim(), lastName!.Trim(),
            NormalizeEmail(email)!, dateOfBirth, DateTimeOffset.UtcNow));
    }

    public Result Update(string? firstName, string? lastName, string? email, DateOnly dateOfBirth)
    {
        var error = Validate(firstName, lastName, email, dateOfBirth);
        if (error is not null) return Result.Failure(error);
        FirstName = firstName!.Trim(); LastName = lastName!.Trim();
        Email = NormalizeEmail(email)!; DateOfBirth = dateOfBirth;
        return Result.Success();
    }

    public static string? NormalizeEmail(string? email) =>
        string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();

    private static Error? Validate(string? firstName, string? lastName, string? email, DateOnly dateOfBirth)
    {
        if (string.IsNullOrWhiteSpace(firstName)) return Invalid("user.first_name_required", "First name is required.");
        if (firstName.Trim().Length > MaxNameLength) return Invalid("user.first_name_too_long", $"Max {MaxNameLength} characters.");
        if (string.IsNullOrWhiteSpace(lastName)) return Invalid("user.last_name_required", "Last name is required.");
        if (lastName.Trim().Length > MaxNameLength) return Invalid("user.last_name_too_long", $"Max {MaxNameLength} characters.");

        var normalizedEmail = NormalizeEmail(email);
        if (normalizedEmail is null || !LooksLikeEmail(normalizedEmail))
            return Invalid("user.email_invalid", "A valid email address is required.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (dateOfBirth > today) return Invalid("user.dob_in_future", "Date of birth cannot be in the future.");
        var age = today.Year - dateOfBirth.Year;
        if (dateOfBirth > today.AddYears(-age)) age--;
        if (age < MinimumAge) return Invalid("user.too_young", $"Must be at least {MinimumAge}.");
        return null;
    }

    private static bool LooksLikeEmail(string email)
    {
        if (email.Length > 254 || email.Any(char.IsWhiteSpace)) return false;
        var at = email.IndexOf('@');
        return at > 0 && at == email.LastIndexOf('@') && email.IndexOf('.', at) > at + 1 && !email.EndsWith('.');
    }

    private static Error Invalid(string code, string message) => new(ErrorType.Validation, code, message);
}