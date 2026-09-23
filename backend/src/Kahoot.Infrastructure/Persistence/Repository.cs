using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Persistence;
using Kahoot.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Infrastructure.Persistence;

internal sealed class Repository<TEntity>(AppDbContext dbContext) : IRepository<TEntity>, IScopedService
    where TEntity : class, IEntity
{
    private const int MaxPageSize = 100;

    public async Task<TEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<TEntity>().FindAsync([id], cancellationToken);
    }

    public async Task<IReadOnlyList<TEntity>> GetAllAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pageNumber, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, MaxPageSize);

        var offset = ((long)pageNumber - 1) * pageSize;
        if (offset > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(pageNumber), "Requested page exceeds the supported range.");
        }

        return await dbContext.Set<TEntity>()
            .AsNoTracking()
            .OrderBy(entity => EF.Property<Guid>(entity, nameof(IEntity.Id)))
            .Skip((int)offset)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        await dbContext.Set<TEntity>().AddAsync(entity, cancellationToken);
    }

    public void Update(TEntity entity)
    {
        dbContext.Set<TEntity>().Update(entity);
    }

    public void Remove(TEntity entity)
    {
        dbContext.Set<TEntity>().Remove(entity);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
