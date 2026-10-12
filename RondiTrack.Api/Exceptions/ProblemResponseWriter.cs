using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace RondiTrack.Api.Exceptions;

// Writes the SAME problem+json shape as RondiTrackExceptionHandler, for the 401/403 responses that
// are produced by the authentication middleware (before any controller or exception handler runs).
public static class ProblemResponseWriter
{
    public static Task WriteAsync(HttpContext httpContext, int status, string code, string detail)
    {
        var correlationId = httpContext.TraceIdentifier;

        var problem = new ProblemDetails
        {
            Type = $"urn:ronditrack:error:{code}",
            Title = code,
            Status = status,
            Detail = detail,
            Instance = httpContext.Request.Path
        };
        problem.Extensions["correlationId"] = correlationId;

        httpContext.Response.StatusCode = status;
        httpContext.Response.Headers["X-Correlation-Id"] = correlationId;

        return httpContext.Response.WriteAsJsonAsync(
            problem, (JsonSerializerOptions?)null, "application/problem+json", httpContext.RequestAborted);
    }
}
