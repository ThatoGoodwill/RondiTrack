using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RondiTrack.Api.Contracts;
using RondiTrack.Api.Tests.TestSupport;
using RondiTrack.Domain;
using RondiTrack.Infrastructure.Data;

namespace RondiTrack.Api.Tests;

// 6.3: what a BROWSER does (preflights, Origin, credentials, retries), not what curl does.
[Collection("Postgres collection")]
public class FrontendContractTests(PostgresApiFactory factory)
{
    private const string Origin = "http://localhost:3000";

    // ---------- helpers ----------

    private static HttpClient NewClient(WebApplicationFactory<Program> host) =>
        host.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = false });

    // A client for a custom host (its own rate limits) that is signed in as the given user.
    private HttpClient SignedIn(WebApplicationFactory<Program> host, TestUser user)
    {
        var client = NewClient(host);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", factory.AccessTokenFor(user));
        return client;
    }

    private static Task<HttpResponseMessage> Record(
        HttpClient client, Guid stokvelId, Guid cycleId, Guid forUserId, decimal amount, string key)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/stokvels/{stokvelId}/contributions")
        {
            Content = JsonContent.Create(new ContributionRequest(forUserId, cycleId, amount))
        };
        request.Headers.Add("Idempotency-Key", key);
        return client.SendAsync(request);
    }

    private async Task<int> CountContributions(Guid userId, Guid cycleId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
        return await db.Contributions.CountAsync(c => c.UserId == userId && c.ContributionCycleId == cycleId);
    }

    private static HttpRequestMessage Preflight(string url, string origin, string requestHeaders)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, url);
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.TryAddWithoutValidation("Access-Control-Request-Headers", requestHeaders);
        return request;
    }

    private async Task<HomeResponse> HomeOf(TestUser user) =>
        (await factory.CreateClientFor(user).GetFromJsonAsync<HomeResponse>("/api/v1/me/home"))!;

    // ---------- CORS ----------

    [Fact]
    public async Task A_preflight_from_the_frontend_asking_for_Idempotency_Key_is_allowed_with_credentials()
    {
        var response = await factory.CreateAnonymousClient().SendAsync(
            Preflight($"/api/v1/stokvels/{Guid.NewGuid()}/contributions", Origin, "content-type, authorization, idempotency-key"));

        Assert.True(response.IsSuccessStatusCode, $"preflight answered {(int)response.StatusCode}");   // no token sent
        Assert.Equal(Origin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal("true", response.Headers.GetValues("Access-Control-Allow-Credentials").Single());
        var allowedHeaders = string.Join(",", response.Headers.GetValues("Access-Control-Allow-Headers"));
        Assert.Contains("Idempotency-Key", allowedHeaders, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_preflight_from_any_other_origin_gets_no_CORS_headers()
    {
        var response = await factory.CreateAnonymousClient().SendAsync(
            Preflight($"/api/v1/stokvels/{Guid.NewGuid()}/contributions", "https://evil.example", "content-type, idempotency-key"));

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }

    [Fact]
    public async Task A_stokvel_scoped_404_sent_with_an_Origin_carries_the_CORS_headers()
    {
        var owner = await factory.CreateUserAsync();
        var stranger = await factory.CreateUserAsync();
        var stokvelId = await factory.CreateStokvelAsync((owner, StokvelMemberRole.Admin));
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/stokvels/{stokvelId}");
        request.Headers.Add("Origin", Origin);

        var response = await factory.CreateClientFor(stranger).SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(Origin, response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.Equal("true", response.Headers.GetValues("Access-Control-Allow-Credentials").Single());
    }

    // ---------- rate limiting ----------

    [Fact]
    public async Task Login_returns_429_problem_json_with_Retry_After_once_the_limit_is_passed()
    {
        using var host = factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("RateLimiting:Login:PermitLimit", "2");
            b.UseSetting("RateLimiting:Login:WindowSeconds", "60");
        });
        var client = NewClient(host);
        var attempt = new LoginRequest($"nobody-{Guid.NewGuid():N}@tests.ronditrack.test", "wrong-password-12345");

        var first = await client.PostAsJsonAsync("/api/v1/auth/login", attempt);
        var second = await client.PostAsJsonAsync("/api/v1/auth/login", attempt);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login") { Content = JsonContent.Create(attempt) };
        request.Headers.Add("Origin", Origin);
        var third = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);      // 429, not the default 503
        Assert.Equal("application/problem+json", third.Content.Headers.ContentType?.MediaType);
        Assert.True(int.Parse(third.Headers.GetValues("Retry-After").Single()) >= 1);
        Assert.Equal(Origin, third.Headers.GetValues("Access-Control-Allow-Origin").Single());   // the browser can read the 429

        var problem = await third.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("detail").GetString()));
        Assert.Matches("^[0-9a-f]{32}$", problem.GetProperty("correlationId").GetString()!);
    }

    [Fact]
    public async Task A_429_does_not_use_up_the_idempotency_key_and_the_contribution_is_recorded_exactly_once()
    {
        // Bucket of 2 that refills 2 tokens every 3 seconds.
        using var host = factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("RateLimiting:Contributions:TokenLimit", "2");
            b.UseSetting("RateLimiting:Contributions:TokensPerPeriod", "2");
            b.UseSetting("RateLimiting:Contributions:ReplenishmentPeriodSeconds", "3");
        });
        var treasurer = await factory.CreateUserAsync();
        var m1 = await factory.CreateUserAsync();
        var m2 = await factory.CreateUserAsync();
        var m3 = await factory.CreateUserAsync();
        var stokvelId = await factory.CreateStokvelAsync(
            (treasurer, StokvelMemberRole.Treasurer), (m1, StokvelMemberRole.Member),
            (m2, StokvelMemberRole.Member), (m3, StokvelMemberRole.Member));
        var cycleId = await factory.CreateCycleAsync(stokvelId);
        var client = SignedIn(host, treasurer);
        var keyK = Guid.NewGuid().ToString();

        // Use up the allowance.
        Assert.Equal(HttpStatusCode.Created, (await Record(client, stokvelId, cycleId, m1.Id, 100m, Guid.NewGuid().ToString())).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Record(client, stokvelId, cycleId, m2.Id, 100m, Guid.NewGuid().ToString())).StatusCode);

        // Key K is rejected with 429. Nothing is recorded for m3.
        var rejected = await Record(client, stokvelId, cycleId, m3.Id, 100m, keyK);
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.True(rejected.Headers.Contains("Retry-After"));
        Assert.Equal(0, await CountContributions(m3.Id, cycleId));

        // Wait out the window, then send K again: it succeeds, because the 429 did not use the key up.
        await Task.Delay(TimeSpan.FromSeconds(3.5));
        var created = await Record(client, stokvelId, cycleId, m3.Id, 100m, keyK);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        // Third time with K: a replay, not a second contribution.
        var replay = await Record(client, stokvelId, cycleId, m3.Id, 100m, keyK);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal("true", replay.Headers.GetValues("Idempotency-Replayed").Single());

        // Exactly one contribution exists for m3 in the database.
        Assert.Equal(1, await CountContributions(m3.Id, cycleId));
    }

    [Fact]
    public async Task One_users_money_limit_does_not_affect_another_user()
    {
        using var host = factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("RateLimiting:Contributions:TokenLimit", "1");
            b.UseSetting("RateLimiting:Contributions:TokensPerPeriod", "1");
            b.UseSetting("RateLimiting:Contributions:ReplenishmentPeriodSeconds", "60");
        });
        var treasurerX = await factory.CreateUserAsync();
        var x1 = await factory.CreateUserAsync();
        var x2 = await factory.CreateUserAsync();
        var stokvelX = await factory.CreateStokvelAsync(
            (treasurerX, StokvelMemberRole.Treasurer), (x1, StokvelMemberRole.Member), (x2, StokvelMemberRole.Member));
        var cycleX = await factory.CreateCycleAsync(stokvelX);

        var treasurerY = await factory.CreateUserAsync();
        var y1 = await factory.CreateUserAsync();
        var stokvelY = await factory.CreateStokvelAsync((treasurerY, StokvelMemberRole.Treasurer), (y1, StokvelMemberRole.Member));
        var cycleY = await factory.CreateCycleAsync(stokvelY);

        var clientX = SignedIn(host, treasurerX);
        var clientY = SignedIn(host, treasurerY);

        Assert.Equal(HttpStatusCode.Created, (await Record(clientX, stokvelX, cycleX, x1.Id, 100m, Guid.NewGuid().ToString())).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await Record(clientX, stokvelX, cycleX, x2.Id, 100m, Guid.NewGuid().ToString())).StatusCode);

        // X is out of tokens; Y has a full bucket of their own.
        Assert.Equal(HttpStatusCode.Created, (await Record(clientY, stokvelY, cycleY, y1.Id, 100m, Guid.NewGuid().ToString())).StatusCode);
    }

    // ---------- home screen ----------

    [Fact]
    public async Task Home_lists_every_stokvel_with_the_callers_role_in_each()
    {
        var me = await factory.CreateUserAsync();
        var other = await factory.CreateUserAsync();
        var first = await factory.CreateStokvelAsync((me, StokvelMemberRole.Member), (other, StokvelMemberRole.Admin));
        var second = await factory.CreateStokvelAsync((me, StokvelMemberRole.Treasurer));

        var home = await HomeOf(me);

        Assert.Equal(2, home.Stokvels.Count);
        Assert.Equal("Member", home.Stokvels.Single(s => s.StokvelId == first).Role);
        Assert.Equal("Treasurer", home.Stokvels.Single(s => s.StokvelId == second).Role);
    }

    [Fact]
    public async Task Home_for_a_user_in_no_stokvels_is_an_empty_list_not_a_404()
    {
        var loner = await factory.CreateUserAsync();

        var response = await factory.CreateClientFor(loner).GetAsync("/api/v1/me/home");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await response.Content.ReadFromJsonAsync<HomeResponse>())!.Stokvels);
    }

    [Fact]
    public async Task Home_computes_paid_and_owed_in_cents_for_the_caller_only()
    {
        var treasurer = await factory.CreateUserAsync();
        var me = await factory.CreateUserAsync();
        var other = await factory.CreateUserAsync();
        var stokvelId = await factory.CreateStokvelAsync(
            (treasurer, StokvelMemberRole.Treasurer), (me, StokvelMemberRole.Member), (other, StokvelMemberRole.Member));
        var cycleId = await factory.CreateCycleAsync(stokvelId);                        // target R100.00 per member
        var client = factory.CreateClientFor(treasurer);
        Assert.Equal(HttpStatusCode.Created, (await Record(client, stokvelId, cycleId, me.Id, 40.00m, Guid.NewGuid().ToString())).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await Record(client, stokvelId, cycleId, other.Id, 70.00m, Guid.NewGuid().ToString())).StatusCode);

        var cycle = (await HomeOf(me)).Stokvels.Single().CurrentCycle!;

        Assert.Equal(4000, cycle.PaidCents);    // only MY R40.00, not the other member's R70.00
        Assert.Equal(6000, cycle.OwedCents);
    }

    // ---------- one error contract ----------

    [Fact]
    public async Task A_validation_failure_returns_an_errors_map_keyed_by_the_camelCase_field_name()
    {
        var body = new RegisterRequest("Test", "User", $"u-{Guid.NewGuid():N}@tests.ronditrack.test", new DateOnly(1990, 1, 1), "short");

        var response = await factory.CreateAnonymousClient().PostAsJsonAsync("/api/v1/auth/register", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.TryGetProperty("password", out _));
        Assert.False(errors.TryGetProperty("Password", out _));
    }

    [Fact]
    public async Task A_number_sent_as_a_string_returns_400_without_an_internal_type_name()
    {
        var treasurer = await factory.CreateUserAsync();
        var member = await factory.CreateUserAsync();
        var stokvelId = await factory.CreateStokvelAsync((treasurer, StokvelMemberRole.Treasurer), (member, StokvelMemberRole.Member));
        var cycleId = await factory.CreateCycleAsync(stokvelId);
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/stokvels/{stokvelId}/contributions")
        {
            Content = new StringContent(
                $"{{\"userId\":\"{member.Id}\",\"contributionCycleId\":\"{cycleId}\",\"amount\":\"100\"}}",
                Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var response = await factory.CreateClientFor(treasurer).SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain("System.", body);
        Assert.DoesNotContain("Exception", body);
        Assert.Equal(0, await CountContributions(member.Id, cycleId));
    }

    [Fact]
    public async Task The_OpenAPI_document_describes_numbers_as_numbers_only()
    {
        var json = await factory.CreateAnonymousClient().GetStringAsync("/openapi/v1.json");
        using var document = JsonDocument.Parse(json);
        var offenders = new List<string>();

        void Walk(JsonElement element, string path)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    if (property.Name == "type" && property.Value.ValueKind == JsonValueKind.Array)
                    {
                        var types = property.Value.EnumerateArray().Select(t => t.GetString()).ToList();
                        if (types.Contains("string") && (types.Contains("integer") || types.Contains("number")))
                            offenders.Add(path);
                    }
                    Walk(property.Value, $"{path}/{property.Name}");
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var item in element.EnumerateArray()) Walk(item, $"{path}/{index++}");
            }
        }

        Walk(document.RootElement, "");
        Assert.Empty(offenders);     // "integer or string" anywhere in the document is a failure
    }

    // ---------- refresh at the versioned path ----------

    [Fact]
    public async Task Refresh_works_at_the_versioned_path_and_the_cookie_path_moved_with_it()
    {
        var user = await factory.CreateUserAsync();
        var client = factory.CreateAnonymousClient();
        var login = await client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(user.Email, user.Password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var setCookie = login.Headers.GetValues("Set-Cookie").Single(h => h.StartsWith("refresh_token="));
        Assert.Contains("path=/api/v1/auth", setCookie, StringComparison.OrdinalIgnoreCase);
        var rawToken = setCookie.Split(';')[0]["refresh_token=".Length..];

        var refresh = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        refresh.Headers.Add("Cookie", $"refresh_token={rawToken}");
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(refresh)).StatusCode);

        var oldPath = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        Assert.NotEqual(HttpStatusCode.OK, (await client.SendAsync(oldPath)).StatusCode);   // the unversioned path is gone
    }
}