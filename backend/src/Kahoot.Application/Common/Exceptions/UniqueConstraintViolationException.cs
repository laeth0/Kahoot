namespace Kahoot.Application.Common.Exceptions;

/// <summary>
/// Represents a database-provider-independent error that occurs when a unique constraint is violated.
/// </summary>
public sealed class UniqueConstraintViolationException : Exception
{
    public string? ConstraintName { get; }

    public UniqueConstraintViolationException(string? constraintName, Exception? innerException)
        : base(
            constraintName is not null
                ? $"A database unique constraint violation occurred on constraint '{constraintName}'."
                : "A database unique constraint violation occurred.",
            innerException)
    {
        ConstraintName = constraintName;
    }

    public UniqueConstraintViolationException(string message, string? constraintName, Exception? innerException)
        : base(message, innerException)
    {
        ConstraintName = constraintName;
    }
}
