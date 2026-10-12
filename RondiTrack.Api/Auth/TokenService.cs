using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using RondiTrack.Domain;

namespace RondiTrack.Api.Auth;

public sealed record AccessToken(string Token, DateTime ExpiresAt);

// Mints the two kinds of token. Holds no per-request state (only options + a clock), so Singleton.
public sealed class TokenService
{
    private readonly JwtOptions _options;
    private readonly TimeProvider _clock;
    private readonly SigningCredentials _signingCredentials;

    public TokenService(IOptions<JwtOptions> options, TimeProvider clock)
    {
        _options = options.Value;
        _clock = clock;
        _signingCredentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
            SecurityAlgorithms.HmacSha256);
    }

    public AccessToken CreateAccessToken(User user) =>
        CreateAccessToken(user.Id, user.IsPlatformAdmin ? PlatformRoles.PlatformAdmin : PlatformRoles.User);

    // The ACCESS token: a signed statement of WHO you are (sub) and your PLATFORM role, valid for
    // minutes. The API trusts it without a database lookup, which is exactly why it carries nothing
    // that must be fresh. NO stokvel role is in here: see StokvelRoleHandler.
    public AccessToken CreateAccessToken(Guid userId, string platformRole)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var expiresAt = now.AddMinutes(_options.AccessTokenMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = userId.ToString(),
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
                [AppClaims.Role] = platformRole
            },
            IssuedAt = now,
            NotBefore = now,
            Expires = expiresAt,
            SigningCredentials = _signingCredentials
        };

        return new AccessToken(new JsonWebTokenHandler().CreateToken(descriptor), expiresAt);
    }

    // The REFRESH token is NOT a JWT: just 64 random bytes. It means nothing on its own; only the
    // database gives it meaning, and that is what makes it revocable.
    public (string RawToken, string TokenHash) CreateRefreshToken()
    {
        var raw = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(64));
        return (raw, HashRefreshToken(raw));
    }

    // SHA-256, not PasswordHasher: the input is 64 random bytes (not a guessable password), so a
    // slow hash buys nothing, and we must be able to look the hash UP, which a salted hash cannot do.
    public static string HashRefreshToken(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
