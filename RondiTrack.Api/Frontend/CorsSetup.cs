using Microsoft.AspNetCore.Cors.Infrastructure;

namespace RondiTrack.Api.Frontend;

public static class CorsPolicies
{
    public const string Frontend = "frontend";
}

public static class CorsSetup
{
    public static IServiceCollection AddFrontendCors(this IServiceCollection services, IConfiguration configuration)
    {
        // Origins come from configuration: [] in appsettings.json, ["http://localhost:3000"] in Development.
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        // Fail at startup, with the fix in the message, if anyone configures a wildcard or a sloppy origin.
        foreach (var origin in origins)
        {
            var exact = Uri.TryCreate(origin, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                && origin == uri.GetLeftPart(UriPartial.Authority)
                && !origin.Contains('*');
            if (!exact)
                throw new InvalidOperationException(
                    $"Cors:AllowedOrigins contains '{origin}'. Use exact origins like https://app.example.com " +
                    "(scheme, host and optional port; no path, no trailing slash, no wildcard).");
        }

        services.AddCors(options => options.AddPolicy(CorsPolicies.Frontend, policy => policy
            .WithOrigins(origins)                                    // exact list; never "*", never echo the request's Origin
            .WithMethods("GET", "POST", "PUT", "DELETE")             // explicit (the preflight itself is OPTIONS)
            .WithHeaders("Content-Type", "Authorization", "Idempotency-Key", "If-Match", "traceparent")
            .WithExposedHeaders("Retry-After", "Idempotency-Replayed", "X-Correlation-Id", "ETag", "Location")
            .AllowCredentials()                                      // the refresh cookie needs it
            .SetPreflightMaxAge(TimeSpan.FromMinutes(10))));

        return services;
    }
}

public static class CorsHeaders
{
    // The exception-handler middleware resets the response before it calls RondiTrackExceptionHandler, which can
    // remove the CORS headers added earlier. Without them the browser hides the error body from the frontend.
    // So the handler asks the CORS service for the same headers again. Same policy, same origin check.
    public static async Task ReapplyAsync(HttpContext httpContext)
    {
        var provider = httpContext.RequestServices.GetRequiredService<ICorsPolicyProvider>();
        var service = httpContext.RequestServices.GetRequiredService<ICorsService>();
        var policy = await provider.GetPolicyAsync(httpContext, CorsPolicies.Frontend);
        if (policy is null) return;
        service.ApplyResult(service.EvaluatePolicy(httpContext, policy), httpContext.Response);
    }
}