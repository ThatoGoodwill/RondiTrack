namespace RondiTrack.Api.Domain;

// ---------------------------------------------------------------------------------
// A person. RICH entity: it protects its own rules.
//  - The constructor is PRIVATE, so the only way to get a User is User.Create(...),
//    which validates first. An invalid User cannot exist.
//  - Setters are private: changes only happen through Update(...), which validates
//    everything BEFORE assigning anything.
// Rules on THIS entity: names required (<=100), valid-looking email, age 18+.
// Email UNIQUENESS is not here: it needs to see all users (UserService).
// ---------------------------------------------------------------------------------
public sealed class User
{
    public const int MaxNameLength = 100;
    public const int MinimumAge = 18;

    public static readonly Error EmailTaken = new(
        ErrorType.Conflict, "user.email_taken", "A user with this email address already exists.");

    private User(Guid id, string firstName, string lastName, string email,
                 DateOnly dateOfBirth, DateTimeOffset createdAt)
    {
        Id = id;
        FirstName = firstName;
        LastName = lastName;
        Email = email;
        DateOfBirth = dateOfBirth;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }                              // set once, never changes
    public string FirstName { get; private set; }
    public string LastName { get; private set; }
    public string Email { get; private set; }
    public DateOnly DateOfBirth { get; private set; }
    public DateTimeOffset CreatedAt { get; }

    // The ONLY door into existence.
    public static Result<User> Create(string? firstName, string? lastName, string? email, DateOnly dateOfBirth)
    {
        var error = Validate(firstName, lastName, email, dateOfBirth);
        if (error is not null) return Result<User>.Failure(error);

        return Result<User>.Success(new User(
            Guid.CreateVersion7(), firstName!.Trim(), lastName!.Trim(),
            NormalizeEmail(email)!, dateOfBirth, DateTimeOffset.UtcNow));
    }

    // Validate EVERYTHING first, then assign: a failed update leaves the user unchanged.
    public Result Update(string? firstName, string? lastName, string? email, DateOnly dateOfBirth)
    {
        var error = Validate(firstName, lastName, email, dateOfBirth);
        if (error is not null) return Result.Failure(error);

        FirstName = firstName!.Trim();
        LastName = lastName!.Trim();
        Email = NormalizeEmail(email)!;
        DateOfBirth = dateOfBirth;
        return Result.Success();
    }

    // Emails are stored trimmed + lowercase so "is it taken?" is a plain equality check.
    public static string? NormalizeEmail(string? email) =>
        string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();

    // ---------------- the rules (shared by Create and Update) ----------------
    private static Error? Validate(string? firstName, string? lastName, string? email, DateOnly dateOfBirth)
    {
        if (string.IsNullOrWhiteSpace(firstName))
            return Invalid("user.first_name_required", "First name is required.");
        if (firstName.Trim().Length > MaxNameLength)
            return Invalid("user.first_name_too_long", $"First name cannot exceed {MaxNameLength} characters.");

        if (string.IsNullOrWhiteSpace(lastName))
            return Invalid("user.last_name_required", "Last name is required.");
        if (lastName.Trim().Length > MaxNameLength)
            return Invalid("user.last_name_too_long", $"Last name cannot exceed {MaxNameLength} characters.");

        var normalizedEmail = NormalizeEmail(email);
        if (normalizedEmail is null || !LooksLikeEmail(normalizedEmail))
            return Invalid("user.email_invalid", "A valid email address is required.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (dateOfBirth > today)
            return Invalid("user.dob_in_future", "Date of birth cannot be in the future.");
        if (AgeOn(today, dateOfBirth) < MinimumAge)
            return Invalid("user.too_young", $"A member must be at least {MinimumAge} years old.");

        return null;   // null means "no error"
    }

    // Subtracting years alone is wrong before the birthday; this corrects for it.
    private static int AgeOn(DateOnly today, DateOnly dateOfBirth)
    {
        var age = today.Year - dateOfBirth.Year;
        if (dateOfBirth > today.AddYears(-age)) age--;
        return age;
    }

    private static bool LooksLikeEmail(string email)
    {
        if (email.Length > 254 || email.Any(char.IsWhiteSpace)) return false;
        var at = email.IndexOf('@');
        return at > 0
            && at == email.LastIndexOf('@')          // exactly one '@'
            && email.IndexOf('.', at) > at + 1       // a dot after the '@', not right next to it
            && !email.EndsWith('.');
    }

    private static Error Invalid(string code, string message) => new(ErrorType.Validation, code, message);
}
