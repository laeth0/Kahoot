using Kahoot.Application.Common.Security;
using Kahoot.Domain.Hosts;
using Kahoot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kahoot.Infrastructure.Startup;

public sealed class DatabaseSeederHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<HostSeedOptions> seedOptions,
    ILogger<DatabaseSeederHostedService> logger) : IHostedLifecycleService
{
    public async Task StartingAsync(CancellationToken cancellationToken)
    {
        HostSeedOptions options = seedOptions.Value;

        if (!options.IsComplete)
        {
            logger.LogWarning(
                "Bootstrap host credentials are not configured ('{Section}'); host seeding skipped.",
                HostSeedOptions.SectionName);
            return;
        }

        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        KahootDbContext context = scope.ServiceProvider.GetRequiredService<KahootDbContext>();
        IPasswordHasher passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        string username = options.Username!.Trim().ToLowerInvariant();

        bool alreadyExists = await context.Hosts.AnyAsync(host => host.Username == username, cancellationToken);
        if (alreadyExists)
        {
            logger.LogInformation("Bootstrap host '{Username}' already exists; host seeding skipped.", username);
            return;
        }

        context.Hosts.Add(new Host
        {
            Username = username,
            PasswordHash = passwordHasher.Hash(options.Password!)
        });

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Bootstrap host '{Username}' created.", username);
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
