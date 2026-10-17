using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using RondiTrack.Api.Auth;
using RondiTrack.Api.Exceptions;

namespace RondiTrack.Api.Frontend;

// Policies are NAMED here and only referenced by endpoints ([EnableRateLimiting(RateLimitPolicies.Login)]).
public static class RateLimitPolicies
{
    public const string Login = "login";
    public const string Contributions = "contributions";
    public const string Payouts = "payouts";
}

public sealed class FixedWindowSettings
{
    public int PermitLimit { get; set; }
    public int WindowSeconds { get; set; }
}

public sealed class TokenBucketSettings
{
    public int TokenLimit { get; set; }                  // the burst: how many requests in a row from a full bucket
    public int TokensPerPeriod { get; set; }             // how many tokens come back every period
    public int ReplenishmentPeriodSeconds { get; set; }
}

public sealed class RateLimitingSettings
{
    public FixedWindowSettings Login { get; set; } = new() { PermitLimit = 10, WindowSeconds = 60 };
    public TokenBucketSettings Contributions { get; set; } = new() { TokenLimit = 30, TokensPerPeriod = 10, ReplenishmentPeriodSeconds = 60 };
    public TokenBucketSettings Payouts { get; set; } = new() { TokenLimit = 10, TokensPerPeriod = 2, ReplenishmentPeriodSeconds = 60 };
}

public static class RateLimitingSetup
{
    public static IServiceCollection AddRondiTrackRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var s = new RateLimitingSettings();
        configuration.GetSection("RateLimiting").Bind(s);
        Validate(s);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;   // not the default 503

            // LOGIN: fixed window, partitioned by client IP address (the caller is not signed in yet).
            options.AddPolicy(RateLimitPolicies.Login, http => RateLimitPartition.GetFixedWindowLimiter(
                ClientIp(http),
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = s.Login.PermitLimit,
                    Window = TimeSpan.FromSeconds(s.Login.WindowSeconds),
                    QueueLimit = 0,               // never queue: a queued money request is a request nobody is watching
                    AutoReplenishment = true
                }));

            // MONEY ENDPOINTS: token bucket, partitioned by the SIGNED-IN USER'S ID (not the IP address).
            options.AddPolicy(RateLimitPolicies.Contributions, http => PerUser(http, s.Contributions));
            options.AddPolicy(RateLimitPolicies.Payouts, http => PerUser(http, s.Payouts));

            // EVERY rejection: 429 + Retry-After + problem+json (title, detail, correlationId from 6.2).
            options.OnRejected = async (context, _) =>
            {
                var seconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                    ? Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
                    : 1;
                context.HttpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);

                await ProblemResponseWriter.WriteAsync(context.HttpContext, StatusCodes.Status429TooManyRequests,
                    "rate_limit.exceeded",
                    "Too many requests. Wait the number of seconds in the Retry-After header, then try again with the same Idempotency-Key.");
            };
        });

        return services;
    }

    private static string ClientIp(HttpContext http) => http.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static RateLimitPartition<string> PerUser(HttpContext http, TokenBucketSettings t)
    {
        // Runs AFTER authentication, so the user is known. No user = the request will get a 401 from authorization anyway.
        var userId = http.User.GetUserId();
        if (userId is null) return RateLimitPartition.GetNoLimiter("anonymous");

        return RateLimitPartition.GetTokenBucketLimiter(userId.Value.ToString(), _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = t.TokenLimit,
            TokensPerPeriod = t.TokensPerPeriod,
            ReplenishmentPeriod = TimeSpan.FromSeconds(t.ReplenishmentPeriodSeconds),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    }

    private static void Validate(RateLimitingSettings s)
    {
        if (s.Login.PermitLimit < 1 || s.Login.WindowSeconds < 1
            || s.Contributions.TokenLimit < 1 || s.Contributions.TokensPerPeriod < 1 || s.Contributions.ReplenishmentPeriodSeconds < 1
            || s.Payouts.TokenLimit < 1 || s.Payouts.TokensPerPeriod < 1 || s.Payouts.ReplenishmentPeriodSeconds < 1)
            throw new InvalidOperationException("Every value under RateLimiting:* must be 1 or more. Check appsettings.json.");
    }
}