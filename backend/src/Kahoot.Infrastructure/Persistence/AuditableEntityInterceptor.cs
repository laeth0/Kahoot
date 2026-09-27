using Kahoot.Application.Common.Interfaces;
using Kahoot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Kahoot.Infrastructure.Persistence;

internal sealed class AuditableEntityInterceptor : SaveChangesInterceptor
{
    private readonly TimeProvider _timeProvider;
    private readonly ICurrentUser _currentUser;

    public AuditableEntityInterceptor(
        TimeProvider timeProvider,
        ICurrentUser currentUser)
    {
        _timeProvider = timeProvider;
        _currentUser = currentUser;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            StampAuditFields(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            StampAuditFields(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    private void StampAuditFields(DbContext context)
    {
        DateTimeOffset utcNow = _timeProvider.GetUtcNow();
        Guid? currentUserId = _currentUser?.UserId;

        foreach (EntityEntry<IAuditableEntity> entry in context.ChangeTracker.Entries<IAuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = utcNow;
                entry.Entity.UpdatedAt = utcNow;

                if (!entry.Entity.CreatedBy.HasValue)
                {
                    entry.Entity.CreatedBy = currentUserId;
                }

                if (!entry.Entity.UpdatedBy.HasValue)
                {
                    entry.Entity.UpdatedBy = currentUserId;
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = utcNow;
                entry.Property(e => e.CreatedAt).IsModified = false;
                entry.Property(e => e.CreatedBy).IsModified = false;

                if (currentUserId.HasValue)
                {
                    entry.Entity.UpdatedBy = currentUserId;
                }
            }
        }
    }
}
