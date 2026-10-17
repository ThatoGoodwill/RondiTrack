using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RondiTrack.Api.Authorization;
using RondiTrack.Api.Contracts;
using RondiTrack.Api.Observability;
using RondiTrack.Api.Tests.TestSupport;
using RondiTrack.Domain;

namespace RondiTrack.Api.Tests;

// 6.2: observability and POPIA, tested as behaviour. None of these tests needs the Aspire dashboard or the dev
// database: the factory starts its own PostgreSQL container and never sets OTEL_EXPORTER_OTLP_ENDPOINT.
[Collection("Postgres collection")]
public class ObservabilityTests(PostgresApiFactory factory)
{
    // ---------- helpers ----------

    // A W3C trace id is 32 lowercase hex characters (and not all zeros).
    private static string NewTraceId() => Guid.NewGuid().ToString("N");

    private static HttpRequestMessage Traced(HttpMethod method, string url, string traceId)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("traceparent", $"00-{traceId}-00f067aa0ba902b7-01");   // 01 = sampled
        return request;
    }

    private static async Task<string?> CorrelationIdOf(HttpResponseMessage response)
    {
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        return problem.GetProperty("correlationId").GetString();
    }

    private static string ReadRefreshCookie(HttpResponseMessage response) =>
        response.Headers.GetValues("Set-Cookie").Single(h => h.StartsWith("refresh_token="))
            .Split(';')[0]["refresh_token=".Length..];

    private static Task<HttpResponseMessage> PostWithCookieAsync(HttpClient client, string url, string rawRefreshToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("Cookie", $"refresh_token={rawRefreshToken}");
        return client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> RecordContributionAsync(
        HttpClient client, Guid stokvelId, Guid cycleId, Guid forUserId, decimal amount, string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/stokvels/{stokvelId}/contributions")
        {
            Content = JsonContent.Create(new ContributionRequest(forUserId, cycleId, amount))
        };
        request.Headers.Add("Idempotency-Key", idempotencyKey ?? Guid.NewGuid().ToString());
        return client.SendAsync(request);
    }

    // A stokvel with a treasurer and one member, one open cycle, and the member's contribution recorded:
    // everything a payout needs.
    private async Task<(TestUser Treasurer, TestUser Member, Guid StokvelId, Guid CycleId)> StokvelReadyForPayoutAsync()
    {
        var treasurer = await factory.CreateUserAsync();
        var member = await factory.CreateUserAsync();
        var stokvelId = await factory.CreateStokvelAsync(
            (treasurer, StokvelMemberRole.Treasurer), (member, StokvelMemberRole.Member));
        var cycleId = await factory.CreateCycleAsync(stokvelId);

        var recorded = await RecordContributionAsync(factory.CreateClientFor(treasurer), stokvelId, cycleId, member.Id, 100m);
        Assert.True(recorded.IsSuccessStatusCode, $"setup: recording the contribution failed with {(int)recorded.StatusCode}");

        return (treasurer, member, stokvelId, cycleId);
    }

    private static string PayoutUrl(Guid stokvelId, Guid cycleId) => $"/api/v1/stokvels/{stokvelId}/cycles/{cycleId}/payout";

    // ---------- health probes ----------

    [Fact]
    public async Task Liveness_answers_without_a_token_and_says_only_Healthy()
    {
        var response = await factory.CreateAnonymousClient().GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Readiness_checks_the_database_and_answers_without_a_token_and_says_only_Healthy()
    {
        var response = await factory.CreateAnonymousClient().GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    // ---------- one correlation id ----------

    [Fact]
    public async Task A_404_for_a_non_member_returns_the_callers_trace_id_and_logs_it()
    {
        var owner = await factory.CreateUserAsync();
        var stranger = await factory.CreateUserAsync();
        var stokvelId = await factory.CreateStokvelAsync((owner, StokvelMemberRole.Admin));
        var traceId = NewTraceId();

        var response = await factory.CreateClientFor(stranger)
            .SendAsync(Traced(HttpMethod.Get, $"/api/v1/stokvels/{stokvelId}", traceId));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(traceId, await CorrelationIdOf(response));
        Assert.Equal(traceId, response.Headers.GetValues("X-Correlation-Id").Single());

        // The SAME id is on the log lines for this request: the refusal itself, and the handler's answer.
        var lines = factory.Logs.Entries.Where(e => e.AllText.Contains(traceId)).ToList();
        Assert.Contains(lines, e => e.Level == LogLevel.Warning && e.Category.EndsWith(nameof(StokvelAccessChecker)));
        Assert.Contains(lines, e => e.Level == LogLevel.Information && e.Category.EndsWith("RondiTrackExceptionHandler"));
    }

    [Fact]
    public async Task An_anonymous_401_returns_the_callers_trace_id_and_logs_it()
    {
        var traceId = NewTraceId();

        var response = await factory.CreateAnonymousClient()
            .SendAsync(Traced(HttpMethod.Get, $"/api/v1/stokvels/{Guid.NewGuid()}", traceId));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(traceId, await CorrelationIdOf(response));
        Assert.Contains(factory.Logs.Entries, e => e.Level == LogLevel.Information && e.AllText.Contains(traceId));
    }

    [Fact]
    public async Task Without_a_traceparent_the_correlation_id_is_still_a_trace_id_not_the_aspnetcore_request_number()
    {
        var response = await factory.CreateAnonymousClient().GetAsync($"/api/v1/stokvels/{Guid.NewGuid()}");

        var correlationId = await CorrelationIdOf(response);

        Assert.Matches("^[0-9a-f]{32}$", correlationId!);   // "0HNP7JI0LHGG9:00000001" would not match
    }

    // ---------- POPIA: nothing personal or secret in the logs ----------

    [Fact]
    public async Task Nothing_sensitive_reaches_the_logs_and_a_409_is_not_an_error()
    {
        const decimal amount = 1234.56m;
        const string password = "Leak-Test-Password-12345";
        var email = $"leak-{Guid.NewGuid():N}@tests.ronditrack.test";
        var anonymous = factory.CreateAnonymousClient();
        var registration = new RegisterRequest("Leak", "Tester", email, new DateOnly(1990, 1, 1), password);

        factory.Logs.Clear();

        // 1. register
        var register = await anonymous.PostAsJsonAsync("/api/v1/auth/register", registration);
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var user = (await register.Content.ReadFromJsonAsync<UserResponse>())!;

        // 2. log in
        var login = await anonymous.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var firstTokens = (await login.Content.ReadFromJsonAsync<TokenResponse>())!;
        var firstRefreshToken = ReadRefreshCookie(login);

        // 3. refresh
        var refresh = await PostWithCookieAsync(anonymous, "/api/v1/auth/refresh", firstRefreshToken);
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var secondTokens = (await refresh.Content.ReadFromJsonAsync<TokenResponse>())!;
        var secondRefreshToken = ReadRefreshCookie(refresh);

        // 4. record a contribution with a distinctive amount
        var stokvelId = await factory.CreateStokvelAsync((new TestUser(user.Id, email, password, false), StokvelMemberRole.Member));
        var cycleId = await factory.CreateCycleAsync(stokvelId);
        var signedIn = factory.CreateAnonymousClient();
        signedIn.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secondTokens.AccessToken);
        var contribution = await RecordContributionAsync(signedIn, stokvelId, cycleId, user.Id, amount);
        Assert.Equal(HttpStatusCode.Created, contribution.StatusCode);

        // 5. register the same email again: a 409, which is the CLIENT's mistake, not ours
        var linesBeforeDuplicate = factory.Logs.Entries.Count;
        var duplicate = await anonymous.PostAsJsonAsync("/api/v1/auth/register", registration);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var duplicateLines = factory.Logs.Entries.Skip(linesBeforeDuplicate).ToList();

        Assert.DoesNotContain(duplicateLines, e => e.Level >= LogLevel.Error);          // no Error for a 409
        Assert.Contains(duplicateLines, e => e.Level == LogLevel.Information
                                              && e.AllText.Contains("409")
                                              && e.AllText.Contains("ConflictException")); // the TYPE is logged, not the message

        // Nothing a person with log access could read may contain any of these.
        var secrets = new Dictionary<string, string>
        {
            ["the email address"] = email,
            ["the password"] = password,
            ["the first access token"] = firstTokens.AccessToken,
            ["the second access token"] = secondTokens.AccessToken,
            ["the first refresh token"] = firstRefreshToken,
            ["the second refresh token"] = secondRefreshToken,
            ["the amount (point)"] = "1234.56",
            ["the amount (comma)"] = "1234,56",
        };

        var entries = factory.Logs.Entries;

        // Guard against a test that passes only because nothing was captured.
        Assert.True(entries.Count >= 5, $"only {entries.Count} log entries were captured");
        Assert.Contains(entries, e => e.Category.EndsWith("AuthService") && e.Message.StartsWith("Login succeeded"));

        foreach (var entry in entries)
            foreach (var (what, secret) in secrets)
                Assert.False(
                    entry.AllText.Contains(secret, StringComparison.OrdinalIgnoreCase),
                    $"A log entry from {entry.Category} contains {what}: {entry.Message}");
    }

    [Fact]
    public async Task A_failed_sign_in_is_logged_without_the_typed_email()
    {
        var existing = await factory.CreateUserAsync();
        var unknownEmail = $"nobody-{Guid.NewGuid():N}@tests.ronditrack.test";
        var anonymous = factory.CreateAnonymousClient();
        factory.Logs.Clear();

        var wrongPassword = await anonymous.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest(existing.Email, "definitely-the-wrong-password"));
        var unknownAccount = await anonymous.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest(unknownEmail, "definitely-the-wrong-password"));

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownAccount.StatusCode);

        var entries = factory.Logs.Entries;
        Assert.Contains(entries, e => e.Message.Contains("Sign-in failed") && e.AllText.Contains(existing.Id.ToString())); // our own id
        Assert.Contains(entries, e => e.Message.Contains("Sign-in failed") && e.Message.Contains("no account matches"));
        Assert.DoesNotContain(entries, e => e.AllText.Contains(existing.Email, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(entries, e => e.AllText.Contains(unknownEmail, StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(entries, e => e.AllText.Contains("definitely-the-wrong-password"));
    }

    [Fact]
    public async Task Refresh_token_reuse_is_a_warning_naming_our_user_id_and_no_token()
    {
        var user = await factory.CreateUserAsync();
        var anonymous = factory.CreateAnonymousClient();
        var login = await anonymous.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest(user.Email, user.Password));
        var oldRefreshToken = ReadRefreshCookie(login);
        Assert.Equal(HttpStatusCode.OK, (await PostWithCookieAsync(anonymous, "/api/v1/auth/refresh", oldRefreshToken)).StatusCode);
        factory.Logs.Clear();

        var replay = await PostWithCookieAsync(anonymous, "/api/v1/auth/refresh", oldRefreshToken);   // the OLD token again

        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        var entries = factory.Logs.Entries;
        Assert.Contains(entries, e => e.Level == LogLevel.Warning
                                      && e.Message.Contains("reuse")
                                      && e.AllText.Contains(user.Id.ToString()));
        Assert.DoesNotContain(entries, e => e.AllText.Contains(oldRefreshToken));
    }

    // ---------- metrics ----------

    [Fact]
    public async Task Processing_a_payout_is_counted_with_the_outcome_success()
    {
        var (treasurer, _, stokvelId, cycleId) = await StokvelReadyForPayoutAsync();
        using var recorder = new MeterRecorder(factory.Services, RondiTrackMetrics.MeterName);

        var response = await factory.CreateClientFor(treasurer).PostAsync(PayoutUrl(stokvelId, cycleId), null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var payouts = recorder.Measurements.Where(m => m.Instrument == RondiTrackMetrics.PayoutsProcessedName).ToList();
        var measurement = Assert.Single(payouts);
        Assert.Equal(1, measurement.Value);
        Assert.Equal(PayoutOutcomes.Success, measurement.Tags[RondiTrackMetrics.OutcomeTag]);
    }

    [Fact]
    public async Task A_refused_payout_is_counted_by_its_outcome_and_its_span_ends_in_error()
    {
        var (treasurer, _, stokvelId, cycleId) = await StokvelReadyForPayoutAsync();
        var client = factory.CreateClientFor(treasurer);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsync(PayoutUrl(stokvelId, cycleId), null)).StatusCode);

        var spans = new ConcurrentQueue<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == RondiTrackTelemetry.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = spans.Enqueue
        };
        ActivitySource.AddActivityListener(listener);
        using var recorder = new MeterRecorder(factory.Services, RondiTrackMetrics.MeterName);

        var second = await client.PostAsync(PayoutUrl(stokvelId, cycleId), null);   // the cycle is already paid out

        Assert.InRange((int)second.StatusCode, 400, 499);   // refused by a business rule (the controller documents 409)
        var counted = Assert.Single(recorder.Measurements, m => m.Instrument == RondiTrackMetrics.PayoutsProcessedName);
        Assert.Equal(PayoutOutcomes.AlreadyProcessed, counted.Tags[RondiTrackMetrics.OutcomeTag]);

        var span = Assert.Single(spans, s => s.GetTagItem(RondiTrackTelemetry.PayoutTags.StokvelId) as string == stokvelId.ToString());
        Assert.Equal(RondiTrackTelemetry.PayoutProcessSpan, span.OperationName);
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal(PayoutOutcomes.AlreadyProcessed, span.GetTagItem(RondiTrackTelemetry.PayoutTags.Outcome));
    }

    [Fact]
    public async Task The_payout_span_carries_only_the_tags_we_chose()
    {
        var (treasurer, _, stokvelId, cycleId) = await StokvelReadyForPayoutAsync();
        var spans = new ConcurrentQueue<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == RondiTrackTelemetry.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = spans.Enqueue
        };
        ActivitySource.AddActivityListener(listener);

        var response = await factory.CreateClientFor(treasurer).PostAsync(PayoutUrl(stokvelId, cycleId), null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var span = Assert.Single(spans, s => s.GetTagItem(RondiTrackTelemetry.PayoutTags.StokvelId) as string == stokvelId.ToString());
        Assert.NotEqual(ActivityStatusCode.Error, span.Status);

        string[] allowed =
        [
            RondiTrackTelemetry.PayoutTags.StokvelId,
            RondiTrackTelemetry.PayoutTags.CycleId,
            RondiTrackTelemetry.PayoutTags.PayoutId,
            RondiTrackTelemetry.PayoutTags.Outcome,
            RondiTrackTelemetry.PayoutTags.ErrorType
        ];
        Assert.All(span.TagObjects, tag => Assert.Contains(tag.Key, allowed));
        Assert.Equal(PayoutOutcomes.Success, span.GetTagItem(RondiTrackTelemetry.PayoutTags.Outcome));
    }

    [Fact]
    public async Task Authorization_refusals_are_counted_by_result()
    {
        var chair = await factory.CreateUserAsync();
        var member = await factory.CreateUserAsync();
        var stranger = await factory.CreateUserAsync();
        var stokvelId = await factory.CreateStokvelAsync((chair, StokvelMemberRole.Admin), (member, StokvelMemberRole.Member));
        using var recorder = new MeterRecorder(factory.Services, RondiTrackMetrics.MeterName);

        var notMember = await factory.CreateClientFor(stranger).GetAsync($"/api/v1/stokvels/{stokvelId}");
        var wrongRole = await factory.CreateClientFor(member)
            .PostAsJsonAsync($"/api/v1/stokvels/{stokvelId}/cycles", new ContributionCycleRequest($"C-{Guid.NewGuid():N}"[..12], 100m));

        Assert.Equal(HttpStatusCode.NotFound, notMember.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, wrongRole.StatusCode);

        var refusals = recorder.Measurements
            .Where(m => m.Instrument == RondiTrackMetrics.AuthorizationRefusalsName)
            .Select(m => (string?)m.Tags[RondiTrackMetrics.ResultTag])
            .ToList();
        Assert.Equal(2, refusals.Count);
        Assert.Single(refusals, r => r == RefusalResults.NotFound);
        Assert.Single(refusals, r => r == RefusalResults.Forbidden);
    }

    [Fact]
    public async Task A_new_contribution_is_counted_and_an_idempotent_replay_is_not()
    {
        var (treasurer, member, stokvelId, _) = await StokvelReadyForPayoutAsync();
        var secondCycle = await factory.CreateCycleAsync(stokvelId);
        var client = factory.CreateClientFor(treasurer);
        var key = Guid.NewGuid().ToString();
        using var recorder = new MeterRecorder(factory.Services, RondiTrackMetrics.MeterName);

        var first = await RecordContributionAsync(client, stokvelId, secondCycle, member.Id, 100m, key);
        var replay = await RecordContributionAsync(client, stokvelId, secondCycle, member.Id, 100m, key);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);   // the stored answer, nothing new recorded
        Assert.Single(recorder.Measurements, m => m.Instrument == RondiTrackMetrics.ContributionsRecordedName);
    }
}
