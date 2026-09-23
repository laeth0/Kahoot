using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Kahoot.Application.Common.Persistence;

public interface IAppDbContext
{
    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
