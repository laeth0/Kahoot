using Kahoot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kahoot.Infrastructure.Startup;

public sealed class DatabaseMigrationHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<DatabaseMigrationHostedService> logger) : IHostedLifecycleService
{
    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        KahootDbContext context = scope.ServiceProvider.GetRequiredService<KahootDbContext>();

        string[] pendingMigrations = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();

        if (pendingMigrations.Length == 0)
        {
            logger.LogInformation("Database schema is up to date; no migrations to apply.");
            return;
        }

        logger.LogInformation(
            "Applying {Count} pending database migration(s): {Migrations}",
            pendingMigrations.Length,
            string.Join(", ", pendingMigrations));

        await context.Database.MigrateAsync(cancellationToken);

        logger.LogInformation("Database migrations applied.");
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
