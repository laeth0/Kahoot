namespace Kahoot.Application.Common.Exceptions;

// Relational Constraint Abstraction - Database-provider-independent exception representing foreign key constraint violations
public sealed class ForeignKeyConstraintViolationException : Exception
{
    // Constraint Identifier - Captures database constraint name for targeted handler filtering
    public string? ConstraintName { get; }

    public ForeignKeyConstraintViolationException(string? constraintName, Exception innerException)
        : base("A database foreign key constraint violation occurred.", innerException)
    {
        ConstraintName = constraintName;
    }
}
