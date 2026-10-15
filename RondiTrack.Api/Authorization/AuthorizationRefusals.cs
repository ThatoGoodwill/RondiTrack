using RondiTrack.Api.Auth;
using RondiTrack.Api.Observability;

namespace RondiTrack.Api.Authorization;

// The platform-role check ([Authorize(Roles = ...)] / the PlatformAdmin policy) is answered by the authentication
// middleware, before any controller or StokvelAccessChecker runs. Its 403 is reported here, so EVERY authorization
// refusal is counted in one place and logged the same way.
public static class AuthorizationRefusals
{
    public static void ReportForbidden(HttpContext httpContext)
    {
        var services = httpContext.RequestServices;
        services.GetRequiredService<RondiTrackMetrics>().AuthorizationRefused(RefusalResults.Forbidden);
        // Information: ordinary users meeting an admin-only route. Our own user id and the ACTION name, nothing else.
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("RondiTrack.Authorization");
        logger.LogInformation(
            "Request refused: platform role required. UserId={UserId} Endpoint={Endpoint} CorrelationId={CorrelationId}",
            httpContext.User.GetUserId(), httpContext.GetEndpoint()?.DisplayName, Correlation.Current);
    }
}
