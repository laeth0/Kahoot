using Microsoft.EntityFrameworkCore;

namespace Kahoot.Application.Common.Abstractions;

public interface IDbExceptionInterpreter
{
    bool IsUniqueViolation(DbUpdateException exception, string? constraintName = null);
}
