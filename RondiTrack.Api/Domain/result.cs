namespace RondiTrack.Api.Domain;

public enum ErrorType
{
    Validation, // the values sent are invalid on their own  -> 400
    NotFound, // the values sent are valid but do not exist in the system -> 404
    Conflict, // the values sent are valid but conflict with existing data -> 409
}

public sealed record Error(ErrorType Type, string Code, string Message);

public class Result{
    private readonly Error? _error;
    protected Result(Error? error) => _error = error;

    public bool IsSuccess => _error is null;

    // Asking a successful result for its error is a programming error, so we throw an exception
    public Error Error => _error ?? throw new InvalidOperationException("A successful result has no error.");

    public static Result Success() => new Result(null);
    public static Result Failure(Error error) => new Result(error);
}

public sealed class Result<T> : Result
{
    private readonly T? _value;
    private Result(T? value, Error? error) : base(error) => _value = value;

    public T Value => IsSuccess ? _value! : throw new InvalidOperationException("A failed result has no value.");

    public static Result<T> Success(T value) => new Result<T>(value, null);
    public static new Result<T> Failure(Error error) => new Result<T>(default, error);
}