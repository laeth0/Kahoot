namespace Kahoot.Application.Common.Results;

public class Result
{
    // Result Pattern Constructor - Enforces strict state invariants between success flags and error types
    protected Result(bool isSuccess, Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        // Success Invariant Guard - Ensures successful results do not carry an error descriptor
        if (isSuccess && error != Error.None)
        {
            throw new InvalidOperationException("A successful result cannot carry an error.");
        }

        // Failure Invariant Guard - Ensures failed results carry an explicit error descriptor
        if (!isSuccess && error.Type == ErrorType.None)
        {
            throw new InvalidOperationException("A failing result requires an error type other than None.");
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    // Success Status - True if the operation completed successfully without business failures
    public bool IsSuccess { get; }

    // Failure Status - True if the operation encountered a domain error or validation failure
    public bool IsFailure => !IsSuccess;

    // Error Descriptor - Carries machine-readable error code, description, and classification
    public Error Error { get; }

    // Successful Result Factory - Returns a parameterless success result
    public static Result Success() => new(true, Error.None);

    // Failed Result Factory - Returns a failed result containing the specified error descriptor
    public static Result Failure(Error error) => new(false, error);

    // Typed Successful Result Factory - Returns a success result carrying the response payload
    public static Result<TValue> Success<TValue>(TValue value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return new(value, true, Error.None);
    }

    // Typed Failed Result Factory - Returns a typed failure result containing the specified error
    public static Result<TValue> Failure<TValue>(Error error) => new(default, false, error);
}
