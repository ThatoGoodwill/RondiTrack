using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RondiTrack.Api.Observability;
using RondiTrack.Domain.Exceptions;

namespace RondiTrack.Api.Exceptions;

// =================================================================================
// THE ONE CENTRALIZED EXCEPTION HANDLER.
//
// This is the ONLY place in the whole API that decides:
//     "which exception  ->  which HTTP status code  ->  which response body  ->  which log line".
// No controller builds a ProblemDetails or picks a status code.
//
// Program.cs registers it (AddExceptionHandler) and turns it on (UseExceptionHandler). From then on, ANY
// exception thrown ANYWHERE in a request (a controller, a service, a filter) lands in TryHandleAsync below.
// =================================================================================
public sealed class RondiTrackExceptionHandler : IExceptionHandler
{
    private readonly ILogger<RondiTrackExceptionHandler> _logger;

    public RondiTrackExceptionHandler(ILogger<RondiTrackExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // ---- CORRELATION ID ----------------------------------------------------
        // 6.2: the request's W3C TRACE ID (the caller's, if they sent a traceparent header). The SAME value goes in
        // (a) the log line, (b) the response body, (c) a response header, and it is the trace's own id in the dashboard.
        var correlationId = Correlation.GetId(httpContext);

        // ---- MAP EXCEPTION -> STATUS ------------------------------------------
        // Order matters only when one class inherits another. ConflictException also matches
        // IdempotencyKeyConflictException (its child), so that one becomes 409 too.
        object? fieldErrors = null;
        int status;
        string code;
        string detail;

        switch (exception)
        {
            case NotFoundException e:
                status = StatusCodes.Status404NotFound; code = e.Code; detail = e.Message; break;

            case AuthenticationFailedException e:           // 6.1: wrong password, unknown email, bad refresh token
                status = StatusCodes.Status401Unauthorized; code = e.Code; detail = e.Message; break;

            case ForbiddenException e:                      // 6.1: a member whose role is not enough
                status = StatusCodes.Status403Forbidden; code = e.Code; detail = e.Message; break;

            case ConflictException e:                       // includes IdempotencyKeyConflictException
                status = StatusCodes.Status409Conflict; code = e.Code; detail = e.Message; break;

            case UnprocessableException e:
                status = StatusCodes.Status422UnprocessableEntity; code = e.Code; detail = e.Message; break;

            case ValidationFailedException e:
                status = StatusCodes.Status400BadRequest; code = e.Code; detail = e.Message; break;

            case FluentValidation.ValidationException ve:   // thrown by our ValidationFilter
                status = StatusCodes.Status400BadRequest;
                code = "validation.failed";
                detail = "One or more fields are invalid.";
                // Group messages by field so a client can show them next to each input.
                fieldErrors = ve.Errors
                    .GroupBy(x => x.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(x => x.ErrorMessage).ToArray());
                break;

            case DbUpdateConcurrencyException:
                status = StatusCodes.Status409Conflict;
                code = "concurrency.stale_version";
                detail = "This record was modified by someone else since you last read it. Fetch it again and retry.";
                break;

            case DbUpdateException { InnerException: PostgresException { SqlState: "23505" } }:
                status = StatusCodes.Status409Conflict;
                code = "database.unique_violation";
                detail = "This operation conflicts with an existing record.";
                break;

            default:                                        // anything we did NOT plan for = a bug
                status = StatusCodes.Status500InternalServerError;
                code = "unexpected_error";
                detail = "An unexpected error occurred."; // never leak internals to the client
                break;
        }

        // ---- LOG FIRST (with the same correlation id the client will receive) --
        if (status >= 500)
            _logger.LogError(exception,
                "Request failed. CorrelationId={CorrelationId} Status={Status} Code={Code}",
                correlationId, status, code);
        else
            _logger.LogWarning(
                "Request rejected. CorrelationId={CorrelationId} Status={Status} Code={Code} Detail={Detail}",
                correlationId, status, code, detail);

        // ---- BUILD THE RFC 9457 problem+json BODY ------------------------------
        var problem = new ProblemDetails
        {
            Type = $"urn:ronditrack:error:{code}",   // stable identifier for this kind of error
            Title = code,
            Status = status,
            Detail = detail,
            Instance = httpContext.Request.Path
        };
        problem.Extensions["correlationId"] = correlationId;
        if (fieldErrors is not null) problem.Extensions["errors"] = fieldErrors;

        httpContext.Response.StatusCode = status;
        httpContext.Response.Headers["X-Correlation-Id"] = correlationId;

        // "application/problem+json" is the media type RFC 9457 requires.
        await httpContext.Response.WriteAsJsonAsync(
            problem, (JsonSerializerOptions?)null, "application/problem+json", cancellationToken);

        return true;   // true = "I handled it, do not pass it on to anyone else"
    }
}
