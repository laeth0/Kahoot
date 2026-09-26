using Kahoot.Application.Common.Seeding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kahoot.Infrastructure.Persistence;

internal sealed class DatabaseSeeder : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public DatabaseSeeder(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var seeders = scope.ServiceProvider.GetServices<ISeeder>();

        foreach (var seeder in seeders)
        {
            await seeder.SeedAsync(cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
