namespace Kahoot.Application.Common.Exceptions;

// Relational Constraint Abstraction - Database-provider-independent exception representing unique constraint collisions
public sealed class UniqueConstraintViolationException : Exception
{
    // Constraint Identifier - Captures database constraint name for targeted handler filtering
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
