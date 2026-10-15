using System.Diagnostics;

namespace RondiTrack.Api.Observability;

// ONE correlation id, from ONE place.
//
// Before 6.2 the id was httpContext.TraceIdentifier: a number ASP.NET Core invents for its own bookkeeping
// ("0HNP7JI0LHGG9:00000001"). It means nothing to a browser, a proxy or another service.
//
// Now it is the request's W3C TRACE ID: 32 hex characters. If the caller sent a `traceparent` header, ASP.NET Core
// continues THEIR trace, so the id they sent is the id we return, log and trace. One id, three places:
//   the error response (what the user quotes), the log line (what support searches), the trace (what the engineer opens).
public static class Correlation
{
    // The current request's trace id, or null when there is no W3C activity (for example, code running outside a request).
    public static string? Current =>
        Activity.Current is { IdFormat: ActivityIdFormat.W3C } activity ? activity.TraceId.ToString() : null;

    // Always returns something: the trace id, and only if no span exists at all, the old ASP.NET Core request number.
    // OpenTelemetry tracing is always registered, so in practice there is always a span.
    public static string GetId(HttpContext httpContext) => Current ?? httpContext.TraceIdentifier;
}
