namespace RondiTrack.Api.Domain;

// ---------------------------------------------------------------------------------
// The CATEGORY of a failure. This is business meaning, NOT an HTTP code.
// The exception handler later turns each category into a status code.
// ---------------------------------------------------------------------------------
public enum ErrorType
{
    Validation,      // a value is invalid on its own                     -> 400
    NotFound,        // the resource the URL is about does not exist      -> 404
    Conflict,        // valid request, clashes with current state         -> 409
    Unprocessable    // well-formed request points at something missing   -> 422
}

// One failure: its category, a stable machine code (e.g. "stokvel.full"), a human message.
public sealed record Error(ErrorType Type, string Code, string Message);

// ---------------------------------------------------------------------------------
// Result = "it worked" OR "it failed and here is why" (never both).
// Domain code returns a Result instead of throwing for EXPECTED problems.
// ---------------------------------------------------------------------------------
public class Result
{
    private readonly Error? _error;

    protected Result(Error? error) => _error = error;

    public bool IsSuccess => _error is null;

    // Asking a successful result for its error is a programming mistake -> throw.
    public Error Error => _error
        ?? throw new InvalidOperationException("A successful result has no error.");

    public static Result Success() => new(null);
    public static Result Failure(Error error) => new(error);
}

// Result that carries a value on success (e.g. a freshly built User).
public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T? value, Error? error) : base(error) => _value = value;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("A failed result has no value.");

    public static Result<T> Success(T value) => new(value, null);

    // "new" intentionally hides Result.Failure because this one returns Result<T>.
    public static new Result<T> Failure(Error error) => new(default, error);
}
