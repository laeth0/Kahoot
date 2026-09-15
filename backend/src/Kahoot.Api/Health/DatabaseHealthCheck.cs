using Kahoot.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Kahoot.Api.Health;

public sealed class DatabaseHealthCheck(IServiceScopeFactory scopeFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<KahootDbContext>();

            bool canConnect = await dbContext.Database.CanConnectAsync(linkedCts.Token);
            if (canConnect)
            {
                return HealthCheckResult.Healthy("Database is healthy.");
            }

            return HealthCheckResult.Unhealthy("Database connection check returned false.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Database connection check threw an exception.", ex);
        }
    }
}
