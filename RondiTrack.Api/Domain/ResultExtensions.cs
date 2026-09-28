using RondiTrack.Api.Exceptions;

namespace RondiTrack.Api.Domain;

// ---------------------------------------------------------------------------------
// THE BRIDGE between the two error styles in RondiTrack:
//   - INSIDE the domain/services we still use Result / Result<T> (a failed Result is
//     a "note passed back"), because it keeps domain code free of exceptions/HTTP.
//   - AT THE BOUNDARY (controllers) we turn a failed Result into a thrown exception,
//     so the centralized handler answers.
//
// Usage in a controller:
//     var user = (await service.CreateUserAsync(...)).ThrowIfFailure();
// If the Result failed -> the matching exception is thrown and the handler takes over.
// If it succeeded      -> you simply get the value back.
// ---------------------------------------------------------------------------------
public static class ResultExtensions
{
    // Map the domain's ErrorType category to the matching exception class.
    public static RondiTrackException ToException(this Error error) => error.Type switch
    {
        ErrorType.NotFound      => new NotFoundException(error.Code, error.Message),
        ErrorType.Conflict      => new ConflictException(error.Code, error.Message),
        ErrorType.Unprocessable => new UnprocessableException(error.Code, error.Message),
        _                       => new ValidationFailedException(error.Code, error.Message)  // Validation
    };

    // Result<T>: return the value on success, throw on failure.
    public static T ThrowIfFailure<T>(this Result<T> result)
    {
        if (!result.IsSuccess) throw result.Error.ToException();
        return result.Value;
    }

    // Result (no value): do nothing on success, throw on failure.
    public static void ThrowIfFailure(this Result result)
    {
        if (!result.IsSuccess) throw result.Error.ToException();
    }
}
