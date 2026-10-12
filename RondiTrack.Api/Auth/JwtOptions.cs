using System.Text;

namespace RondiTrack.Api.Auth;

// Bound from the "Jwt" configuration section.
//   Issuer, Audience, lifetimes -> appsettings.json (not secret)
//   SigningKey                  -> user-secrets ONLY: whoever holds it can mint a valid token for anyone.
public class JwtOptions
{
    public const string SectionName = "Jwt";
    public const int MinimumKeyBytes = 32;

    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public string SigningKey { get; init; } = string.Empty;
    public int AccessTokenMinutes { get; init; } = 10;
    public int RefreshTokenDays { get; init; } = 7;

    // Called once at startup. A missing key is a startup error with the fix in the message,
    // not a 500 on the first login.
    public void EnsureValid()
    {
        if (Encoding.UTF8.GetByteCount(SigningKey) < MinimumKeyBytes)
            throw new InvalidOperationException(
                $"Jwt:SigningKey is missing or shorter than {MinimumKeyBytes} bytes. Set it with: " +
                "dotnet user-secrets set \"Jwt:SigningKey\" \"<at least 32 random characters>\" --project RondiTrack.Api");

        if (string.IsNullOrWhiteSpace(Issuer) || string.IsNullOrWhiteSpace(Audience))
            throw new InvalidOperationException("Jwt:Issuer and Jwt:Audience must be set in appsettings.json.");

        if (AccessTokenMinutes is < 1 or > 15)
            throw new InvalidOperationException("Jwt:AccessTokenMinutes must be between 1 and 15.");
    }
}
