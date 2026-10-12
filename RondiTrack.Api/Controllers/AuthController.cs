using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RondiTrack.Api.Auth;
using RondiTrack.Api.Contracts;
using RondiTrack.Domain;

namespace RondiTrack.Api.Controllers;

// The only controller anyone can call without a token: you cannot be asked for a token on the
// endpoint that gives you one. HTTP only; decisions live in AuthService.
[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public class AuthController(AuthService authService) : ApiControllerBase
{
    private const string RefreshCookieName = "refresh_token";

    [HttpPost("register")]
    [EndpointSummary("Create an account")]
    [EndpointDescription("Registers a user with a password of at least 12 characters. 409 if the email is taken.")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        var user = (await authService.RegisterAsync(request, ct)).ThrowIfFailure();
        return StatusCode(StatusCodes.Status201Created, UserResponse.FromEntity(user));
    }

    [HttpPost("login")]
    [EndpointSummary("Sign in with email and password")]
    [EndpointDescription("Returns an access token in the body. The refresh token is set as an HttpOnly cookie.")]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TokenResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await authService.LoginAsync(request, ct);
        SetRefreshCookie(result.RefreshToken, result.RefreshTokenExpiresAt);
        return Ok(result.Tokens);
    }

    [HttpPost("refresh")]
    [EndpointSummary("Swap the refresh cookie for a new access token and a new refresh token")]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TokenResponse>> Refresh(CancellationToken ct)
    {
        var result = await authService.RefreshAsync(Request.Cookies[RefreshCookieName], ct);
        SetRefreshCookie(result.RefreshToken, result.RefreshTokenExpiresAt);
        return Ok(result.Tokens);
    }

    [HttpPost("logout")]
    [EndpointSummary("Sign out: revoke this session's refresh tokens and clear the cookie")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await authService.LogoutAsync(Request.Cookies[RefreshCookieName], ct);
        Response.Cookies.Delete(RefreshCookieName, BuildCookieOptions(expires: null));
        return NoContent();
    }

    private void SetRefreshCookie(string rawToken, DateTime expiresAt) =>
        Response.Cookies.Append(RefreshCookieName, rawToken, BuildCookieOptions(new DateTimeOffset(expiresAt, TimeSpan.Zero)));

    // HttpOnly: JavaScript cannot read it, so an XSS bug cannot steal it.
    // Secure: only ever sent over HTTPS.
    // SameSite=Strict: never sent on a request another site triggers (CSRF).
    // Path=/api/auth: only sent to the three endpoints above, never with ordinary API calls.
    private static CookieOptions BuildCookieOptions(DateTimeOffset? expires) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/api/auth",
        Expires = expires
    };
}
