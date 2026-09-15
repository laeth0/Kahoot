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

        if (!options.Enabled)
        {
            logger.LogInformation("Host bootstrap seeding is disabled ('{Section}:Enabled' is false); skipping.", HostSeedOptions.SectionName);
            return;
        }

        if (string.IsNullOrWhiteSpace(options.Username) || string.IsNullOrWhiteSpace(options.Password))
        {
            throw new InvalidOperationException("Host seeding failed: 'Seeding:Host:Username' and 'Seeding:Host:Password' must be provided when seeding is enabled.");
        }

        string password = options.Password;
        if (password.Length < 12 ||
            !password.Any(char.IsUpper) ||
            !password.Any(char.IsLower) ||
            !password.Any(char.IsDigit) ||
            !password.Any(ch => !char.IsLetterOrDigit(ch)))
        {
            throw new InvalidOperationException("Host seeding failed: Seed password does not meet complexity requirements (minimum 12 characters, including uppercase, lowercase, digit, and special character).");
        }

        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        KahootDbContext context = scope.ServiceProvider.GetRequiredService<KahootDbContext>();
        IPasswordHasher passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        string username = options.Username.Trim().ToLowerInvariant();

        bool alreadyExists = await context.Hosts.AnyAsync(host => host.Username == username, cancellationToken);
        if (alreadyExists)
        {
            logger.LogInformation("Bootstrap host '{Username}' already exists; skipping host creation.", username);
            return;
        }

        context.Hosts.Add(new Host
        {
            Username = username,
            PasswordHash = passwordHasher.Hash(password)
        });

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Bootstrap host '{Username}' created successfully.", username);
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
