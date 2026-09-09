using Kahoot.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Kahoot.Infrastructure.Persistence.Interceptors;

public sealed class AuditableEntityInterceptor(TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        StampAuditableEntities(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        StampAuditableEntities(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void StampAuditableEntities(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        DateTime nowUtc = timeProvider.GetUtcNow().UtcDateTime;

        foreach (EntityEntry<AuditableEntity> entry in context.ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State is EntityState.Added)
            {
                entry.Property(e => e.CreatedAt).CurrentValue = nowUtc;
                entry.Property(e => e.UpdatedAt).CurrentValue = nowUtc;
            }
            else if (entry.State is EntityState.Modified)
            {
                entry.Property(e => e.UpdatedAt).CurrentValue = nowUtc;
            }
        }
    }
}
