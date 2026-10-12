using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using RondiTrack.Api.Contracts;
using RondiTrack.Api.Tests.TestSupport;

namespace RondiTrack.Api.Tests;

// 6.1: authentication = "who are you?", tested as behaviour a client can observe: status codes and cookies.
[Collection("Postgres collection")]
public class AuthTests
{
    private readonly PostgresApiFactory _factory;
    private readonly HttpClient _anonymous;

    public AuthTests(PostgresApiFactory factory)
    {
        _factory = factory;
        _anonymous = factory.CreateAnonymousClient();
    }

    // ---------- helpers ----------

    private static string NewEmail() => $"reg-{Guid.NewGuid():N}@tests.ronditrack.test";

    private static RegisterRequest NewRegistration(string? email = null, string? password = null) =>
        new("Lerato", "Mokoena", email ?? NewEmail(), new DateOnly(1992, 7, 21), password ?? PostgresApiFactory.DefaultPassword);

    private Task<HttpResponseMessage> LoginAsync(string email, string password) =>
        _anonymous.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));

    private static string ReadRefreshCookie(HttpResponseMessage response)
    {
        var setCookie = response.Headers.GetValues("Set-Cookie").Single(h => h.StartsWith("refresh_token="));
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/auth", setCookie, StringComparison.OrdinalIgnoreCase);
        return setCookie.Split(';')[0]["refresh_token=".Length..];
    }

    private Task<HttpResponseMessage> PostWithCookieAsync(string url, string rawRefreshToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("Cookie", $"refresh_token={rawRefreshToken}");
        return _anonymous.SendAsync(request);
    }

    private static async Task<JsonElement> ReadProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    // Signs up a fresh user and logs in; returns the raw refresh token from the cookie.
    private async Task<(string Email, string RefreshToken, TokenResponse Tokens)> RegisterAndLoginAsync()
    {
        var email = NewEmail();
        var register = await _anonymous.PostAsJsonAsync("/api/auth/register", NewRegistration(email));
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var login = await LoginAsync(email, PostgresApiFactory.DefaultPassword);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var tokens = (await login.Content.ReadFromJsonAsync<TokenResponse>())!;
        return (email, ReadRefreshCookie(login), tokens);
    }

    // ---------- fallback policy ----------

    [Fact]
    public async Task No_token_is_401_problem_json()
    {
        var response = await _anonymous.GetAsync($"/api/stokvels/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = await ReadProblemAsync(response);
        Assert.Equal(401, problem.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task A_garbage_token_is_401_problem_json()
    {
        var client = _factory.CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "not.a.token");

        var response = await client.GetAsync($"/api/stokvels/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await ReadProblemAsync(response);
    }

    // ---------- registration ----------

    [Fact]
    public async Task Register_creates_an_account_and_never_returns_the_password_or_hash()
    {
        var response = await _anonymous.PostAsJsonAsync("/api/auth/register", NewRegistration());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_with_a_short_password_is_400_problem_json()
    {
        var response = await _anonymous.PostAsJsonAsync("/api/auth/register", NewRegistration(password: "short-pw-1"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await ReadProblemAsync(response);
    }

    [Fact]
    public async Task Register_with_the_same_email_in_a_different_case_is_409()
    {
        var email = NewEmail();
        var first = await _anonymous.PostAsJsonAsync("/api/auth/register", NewRegistration(email));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await _anonymous.PostAsJsonAsync("/api/auth/register", NewRegistration(email.ToUpperInvariant()));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        await ReadProblemAsync(second);
    }

    // ---------- login ----------

    [Fact]
    public async Task Login_returns_a_short_lived_access_token_and_the_refresh_token_only_in_a_cookie()
    {
        var email = NewEmail();
        await _anonymous.PostAsJsonAsync("/api/auth/register", NewRegistration(email));

        var response = await LoginAsync(email, PostgresApiFactory.DefaultPassword);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var refresh = ReadRefreshCookie(response);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(refresh, raw);                                   // never in the body

        var jwt = new JsonWebToken((await response.Content.ReadFromJsonAsync<TokenResponse>())!.AccessToken);
        Assert.True(jwt.ValidTo - jwt.ValidFrom <= TimeSpan.FromMinutes(15));  // assignment: 15 minutes or less
        Assert.False(jwt.TryGetClaim("stokvel_role", out _));                  // no per-stokvel role in the token
        Assert.False(jwt.TryGetClaim("email", out _));
    }

    [Fact]
    public async Task Refresh_token_is_stored_only_as_a_hash()
    {
        var (_, refreshToken, _) = await RegisterAndLoginAsync();

        Assert.False(await _factory.AnyRefreshTokenHashEqualsAsync(refreshToken));
        Assert.True(await _factory.AnyRefreshTokenHashEqualsAsync(RondiTrack.Api.Auth.TokenService.HashRefreshToken(refreshToken)));
    }

    [Fact]
    public async Task Wrong_password_and_unknown_email_return_identical_problem_details()
    {
        var email = NewEmail();
        await _anonymous.PostAsJsonAsync("/api/auth/register", NewRegistration(email));

        var wrongPassword = await LoginAsync(email, "Definitely-Wrong-Pass-9");
        var unknownEmail = await LoginAsync(NewEmail(), "Definitely-Wrong-Pass-9");

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);

        var a = await ReadProblemAsync(wrongPassword);
        var b = await ReadProblemAsync(unknownEmail);
        foreach (var field in new[] { "type", "title", "status", "detail", "instance" })
            Assert.Equal(a.GetProperty(field).ToString(), b.GetProperty(field).ToString());
        // (correlationId is the only field allowed to differ: it is different on every request.)
    }

    // ---------- refresh: rotation + reuse detection ----------

    [Fact]
    public async Task Refresh_rotates_the_token_and_replaying_the_old_one_kills_the_newer_one_too()
    {
        var (_, first, _) = await RegisterAndLoginAsync();

        // 1. Using the first token works and gives a DIFFERENT second token.
        var rotated = await PostWithCookieAsync("/api/auth/refresh", first);
        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        var second = ReadRefreshCookie(rotated);
        Assert.NotEqual(first, second);

        // 2. Replaying the FIRST token is reuse: 401...
        var replay = await PostWithCookieAsync("/api/auth/refresh", first);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        await ReadProblemAsync(replay);

        // 3. ...and the whole family is dead, so the legitimate SECOND token is rejected as well.
        var newer = await PostWithCookieAsync("/api/auth/refresh", second);
        Assert.Equal(HttpStatusCode.Unauthorized, newer.StatusCode);
    }

    [Fact]
    public async Task Refresh_without_a_cookie_is_401()
    {
        var response = await _anonymous.PostAsync("/api/auth/refresh", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------- logout ----------

    [Fact]
    public async Task After_logout_refreshing_is_401_and_logout_is_idempotent()
    {
        var (_, refreshToken, _) = await RegisterAndLoginAsync();

        var logout = await PostWithCookieAsync("/api/auth/logout", refreshToken);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var refresh = await PostWithCookieAsync("/api/auth/refresh", refreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);

        var logoutAgain = await PostWithCookieAsync("/api/auth/logout", refreshToken);   // same token, already revoked
        Assert.Equal(HttpStatusCode.NoContent, logoutAgain.StatusCode);

        var logoutWithNothing = await _anonymous.PostAsync("/api/auth/logout", null);    // no cookie at all
        Assert.Equal(HttpStatusCode.NoContent, logoutWithNothing.StatusCode);
    }
}
