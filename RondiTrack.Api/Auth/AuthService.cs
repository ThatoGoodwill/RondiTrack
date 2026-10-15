using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using RondiTrack.Api.Contracts;
using RondiTrack.Api.Observability;
using RondiTrack.Domain;
using RondiTrack.Domain.Exceptions;
using RondiTrack.Domain.Repositories;

namespace RondiTrack.Api.Auth;

// What a successful login/refresh hands back to the controller: the access token for the body,
// and the RAW refresh token for the cookie (never for the body).
public sealed record AuthResult(TokenResponse Tokens, string RefreshToken, DateTime RefreshTokenExpiresAt);

// The controller deals with HTTP (cookies, status codes). Every DECISION about signing in lives here.
// Logging rule: ids only. Never an email, a password, a token or a hash.
public sealed class AuthService(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IPasswordHasher<User> passwordHasher,
    TokenService tokenService,
    IOptions<JwtOptions> options,
    TimeProvider clock,
    RondiTrackMetrics metrics,
    ILogger<AuthService> logger)
{
    // Hashed once at startup. Used when the email does not exist (or the account has no password),
    // so a wrong email takes as long as a wrong password. Otherwise response TIME tells an attacker
    // which emails have accounts.
    private static readonly string UnknownUserHash =
        new PasswordHasher<User>().HashPassword(null!, "unknown-user-timing-equaliser");

    private TimeSpan RefreshLifetime => TimeSpan.FromDays(options.Value.RefreshTokenDays);
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    // ONE message for "no such email" and "wrong password".
    private static AuthenticationFailedException BadCredentials() =>
        new("auth.invalid_credentials", "Email or password is incorrect.");

    public async Task<Result<User>> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        // Rule 1 (entity): names, email format, 18+.
        var created = User.Create(request.FirstName, request.LastName, request.Email, request.DateOfBirth);
        if (!created.IsSuccess) return created;
        var user = created.Value;

        // Rule 2 (needs the database): one account per (normalised) email. The unique index is the
        // real guarantee; this check just gives the friendly 409 in the normal case.
        if (await users.EmailExistsAsync(user.Email, null, ct))
            return Result<User>.Failure(User.EmailTaken);

        user.SetPasswordHash(passwordHasher.HashPassword(user, request.Password!));
        await users.AddAsync(user, ct);

        logger.LogInformation("User registered. UserId={UserId}", user.Id);
        return Result<User>.Success(user);
    }

    public async Task<AuthResult> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await users.GetByEmailAsync(request.Email!, ct);
        var hash = user is null || string.IsNullOrEmpty(user.PasswordHash) ? UnknownUserHash : user.PasswordHash;

        var verification = passwordHasher.VerifyHashedPassword(user!, hash, request.Password!);

        if (user is null || string.IsNullOrEmpty(user.PasswordHash) || verification == PasswordVerificationResult.Failed)
        {
            logger.LogWarning("Login failed.");
            metrics.SignInAttempted(succeeded: false);
            throw BadCredentials();
        }

        var (rawToken, tokenHash) = tokenService.CreateRefreshToken();
        var refreshToken = RefreshToken.StartFamily(user.Id, tokenHash, Now, RefreshLifetime);
        await refreshTokens.AddAsync(refreshToken, ct);

        logger.LogInformation("Login succeeded. UserId={UserId} FamilyId={FamilyId}", user.Id, refreshToken.FamilyId);
        metrics.SignInAttempted(succeeded: true);
        return Issue(user, rawToken, refreshToken);
    }

    public async Task<AuthResult> RefreshAsync(string? rawRefreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(rawRefreshToken))
            throw new AuthenticationFailedException("auth.refresh_missing", "No refresh token was sent.");

        var now = Now;
        var current = await refreshTokens.GetByHashAsync(TokenService.HashRefreshToken(rawRefreshToken), ct)
            ?? throw new AuthenticationFailedException("auth.refresh_invalid", "Refresh token is not recognised. Please sign in again.");

        if (current.RevokedAt is not null)
        {
            // REUSE DETECTED. This token was already spent (rotated) or revoked, yet someone presented
            // it again. Either the real user's app replayed an old token or an attacker stole one; the
            // server cannot tell which. So the WHOLE family dies and the real user signs in again.
            await refreshTokens.RevokeFamilyAsync(current.FamilyId, now, ct);
            logger.LogWarning("Refresh token reuse detected; family revoked. UserId={UserId} FamilyId={FamilyId}",
                current.UserId, current.FamilyId);
            metrics.RefreshTokenReuseDetected();
            throw new AuthenticationFailedException("auth.refresh_reused", "Refresh token was already used. Please sign in again.");
        }

        if (!current.IsActive(now))
            throw new AuthenticationFailedException("auth.refresh_expired", "Refresh token has expired. Please sign in again.");

        var user = await users.GetByIdAsync(current.UserId, ct)
            ?? throw new AuthenticationFailedException("auth.refresh_invalid", "Refresh token is not recognised. Please sign in again.");

        // ROTATION: spend the old token, issue its successor, save both together.
        var (rawToken, tokenHash) = tokenService.CreateRefreshToken();
        var successor = current.Rotate(tokenHash, now, RefreshLifetime);
        await refreshTokens.SaveRotationAsync(successor, ct);

        logger.LogInformation("Refresh token rotated. UserId={UserId} FamilyId={FamilyId}", user.Id, current.FamilyId);
        return Issue(user, rawToken, successor);
    }

    public async Task LogoutAsync(string? rawRefreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(rawRefreshToken)) return;   // already signed out: logout is idempotent

        var current = await refreshTokens.GetByHashAsync(TokenService.HashRefreshToken(rawRefreshToken), ct);
        if (current is null) return;

        await refreshTokens.RevokeFamilyAsync(current.FamilyId, Now, ct);
        logger.LogInformation("Logout. UserId={UserId} FamilyId={FamilyId}", current.UserId, current.FamilyId);
    }

    private AuthResult Issue(User user, string rawRefreshToken, RefreshToken refreshToken)
    {
        var access = tokenService.CreateAccessToken(user);
        return new AuthResult(new TokenResponse(access.Token, access.ExpiresAt), rawRefreshToken, refreshToken.ExpiresAt);
    }
}
