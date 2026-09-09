using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kahoot.Infrastructure.Persistence;

public sealed class DbExceptionInterpreter : IDbExceptionInterpreter, ISingletonService
{
    private const string UniqueViolationSqlState = "23505";

    public bool IsUniqueViolation(DbUpdateException exception, string? constraintName = null)
    {
        if (exception.InnerException is not PostgresException postgres
            || postgres.SqlState != UniqueViolationSqlState)
        {
            return false;
        }

        if (constraintName is null || string.IsNullOrEmpty(postgres.ConstraintName))
        {
            return true;
        }

        return string.Equals(postgres.ConstraintName, constraintName, StringComparison.Ordinal);
    }
}
