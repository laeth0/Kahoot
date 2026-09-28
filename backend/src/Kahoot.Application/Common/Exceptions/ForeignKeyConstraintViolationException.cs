namespace Kahoot.Application.Common.Exceptions;

public sealed class ForeignKeyConstraintViolationException : Exception
{
    public string? ConstraintName { get; }

    public ForeignKeyConstraintViolationException(string? constraintName, Exception innerException)
        : base("A database foreign key constraint violation occurred.", innerException)
    {
        ConstraintName = constraintName;
    }
}
