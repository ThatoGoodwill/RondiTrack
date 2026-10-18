using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using RondiTrack.Api.Contracts;
using RondiTrack.Api.Jobs;
using RondiTrack.Api.Tests.TestSupport;
using RondiTrack.Domain;
using RondiTrack.Infrastructure.Data;

namespace RondiTrack.Api.Tests;

// 6.4: retention. Every delete removes evidence that a feature relies on, so each is tested against that feature.
[Collection("Postgres collection")]
public class HousekeepingTests(PostgresApiFactory factory)
{
    private HousekeepingJob NewJob(TimeProvider clock) => new(
        factory.Services.GetRequiredService<IServiceScopeFactory>(),
        clock,
        Options.Create(new HousekeepingOptions()),          // N = 30 days, M = 7 days
        factory.Services.GetRequiredService<JobMetrics>(),
        NullLogger<HousekeepingJob>.Instance);

    private static string CookieOf(HttpResponseMessage response)
    {
        var setCookie = response.Headers.GetValues("Set-Cookie").Single(h => h.StartsWith("refresh_token="));
        return setCookie.Split(';')[0]["refresh_token=".Length..];
    }

    private static Task<HttpResponseMessage> RefreshWith(HttpClient client, string rawToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        request.Headers.Add("Cookie", $"refresh_token={rawToken}");
        return client.SendAsync(request);
    }

    [Fact]
    public async Task A_refresh_token_inside_its_retention_period_survives_and_one_past_it_is_deleted()
    {
        var user = await factory.CreateUserAsync();
        var now = DateTimeOffset.UtcNow;
        string inside = Guid.NewGuid().ToString("N"), past = Guid.NewGuid().ToString("N");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
            // 7-day tokens: created 12 days ago => expired 5 days ago (inside N=30). Created 47 days ago => expired 40 days ago.
            db.RefreshTokens.Add(RefreshToken.StartFamily(user.Id, inside, now.UtcDateTime.AddDays(-12), TimeSpan.FromDays(7)));
            db.RefreshTokens.Add(RefreshToken.StartFamily(user.Id, past, now.UtcDateTime.AddDays(-47), TimeSpan.FromDays(7)));
            await db.SaveChangesAsync();
        }

        await NewJob(new FakeTimeProvider(now)).RunOnceAsync();

        Assert.True(await factory.AnyRefreshTokenHashEqualsAsync(inside));
        Assert.False(await factory.AnyRefreshTokenHashEqualsAsync(past));
    }

    [Fact]
    public async Task Reuse_detection_still_works_on_a_token_that_expired_yesterday()
    {
        var anon = factory.CreateAnonymousClient();
        var email = $"hk-{Guid.NewGuid():N}@tests.ronditrack.test";
        var register = await anon.PostAsJsonAsync("/api/v1/auth/register",
            new RegisterRequest("Hk", "Tester", email, new DateOnly(1990, 1, 1), PostgresApiFactory.DefaultPassword));
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var login = await anon.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, PostgresApiFactory.DefaultPassword));
        var oldToken = CookieOf(login);
        Assert.Equal(HttpStatusCode.OK, (await RefreshWith(anon, oldToken)).StatusCode);   // rotation: oldToken is now SPENT

        async Task AgeSpentTokens(int daysAgo)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
            var userId = await db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync();
            var expiredAt = DateTime.UtcNow.AddDays(-daysAgo);
            await db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt != null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.ExpiresAt, expiredAt));
        }

        // Expired YESTERDAY: inside the retention period, so housekeeping keeps it and reuse is still recognised.
        await AgeSpentTokens(1);
        await NewJob(TimeProvider.System).RunOnceAsync();
        var replay = await RefreshWith(anon, oldToken);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.Equal("auth.refresh_reused", (await replay.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString());

        // Expired 40 days ago: past retention, so it is deleted and the replay is just "not recognised" (the alarm is lost).
        await AgeSpentTokens(40);
        await NewJob(TimeProvider.System).RunOnceAsync();
        var late = await RefreshWith(anon, oldToken);
        Assert.Equal(HttpStatusCode.Unauthorized, late.StatusCode);
        Assert.Equal("auth.refresh_invalid", (await late.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString());
    }

    [Fact]
    public async Task An_idempotency_record_inside_its_retention_period_still_replays_and_one_past_it_is_deleted()
    {
        var treasurer = await factory.CreateUserAsync();
        var member = await factory.CreateUserAsync();
        var stokvelId = await factory.CreateStokvelAsync((treasurer, StokvelMemberRole.Treasurer), (member, StokvelMemberRole.Member));
        var cycleId = await factory.CreateCycleAsync(stokvelId);
        var client = factory.CreateClientFor(treasurer);
        var key = Guid.NewGuid().ToString();

        Task<HttpResponseMessage> Send()
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/stokvels/{stokvelId}/contributions")
            { Content = JsonContent.Create(new ContributionRequest(member.Id, cycleId, 100m)) };
            request.Headers.Add("Idempotency-Key", key);
            return client.SendAsync(request);
        }

        async Task AgeKey(int daysAgo)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
            var createdAt = DateTimeOffset.UtcNow.AddDays(-daysAgo);
            await db.IdempotencyKeys.Where(k => k.Key == key).ExecuteUpdateAsync(s => s.SetProperty(k => k.CreatedAt, createdAt));
        }

        Assert.Equal(HttpStatusCode.Created, (await Send()).StatusCode);

        await AgeKey(3);                                           // inside M = 7 days
        await NewJob(TimeProvider.System).RunOnceAsync();
        var replay = await Send();
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);        // still a replay
        Assert.Equal("true", replay.Headers.GetValues("Idempotency-Replayed").Single());

        await AgeKey(10);                                          // past M
        await NewJob(TimeProvider.System).RunOnceAsync();
        var late = await Send();
        Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);    // record gone: the unique index still refuses a second payment

        using var check = factory.Services.CreateScope();
        var db2 = check.ServiceProvider.GetRequiredService<RondiTrackDbContext>();
        Assert.Equal(1, await db2.Contributions.CountAsync(c => c.UserId == member.Id && c.ContributionCycleId == cycleId));
    }
}