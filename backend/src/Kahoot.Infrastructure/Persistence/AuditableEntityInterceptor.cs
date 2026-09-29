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

    // Automated Audit Field Population - Intercepts state changes to stamp CreatedAt, UpdatedAt, CreatedBy, UpdatedBy
    private void StampAuditFields(DbContext context)
    {
        // Centralized UTC Clock - Queries injected TimeProvider for deterministic UTC timestamps
        DateTimeOffset utcNow = _timeProvider.GetUtcNow();
        Guid? currentUserId = _currentUser?.UserId;

        // Change Tracker Enumeration - Scans entity tracker for entities implementing IAuditableEntity
        foreach (EntityEntry<IAuditableEntity> entry in context.ChangeTracker.Entries<IAuditableEntity>())
        {
            // Creation Timestamp & Actor - Sets initial timestamps and defaults actor ID to ambient caller
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
            // Mutation Timestamp & Invariant Protection - Updates UpdatedAt while protecting creation metadata from overwrite
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = utcNow;
                // Creation Immutability - Ensures CreatedAt and CreatedBy are never overwritten on updates
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
