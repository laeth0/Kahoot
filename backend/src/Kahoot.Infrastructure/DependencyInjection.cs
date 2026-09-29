using Kahoot.Application.Common.Interfaces;
using Kahoot.Infrastructure.Persistence;
using Kahoot.Infrastructure.Realtime;
using Kahoot.Infrastructure.ServiceCollectionExtension;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kahoot.Infrastructure;

// Infrastructure Dependency Injection Registrar - Composes persistence, security, storage, realtime, and background worker infrastructure.
public static class DependencyInjection
{
    // Infrastructure Service Registration - Binds all infrastructure sub-modules and registers singleton system TimeProvider.
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddPersistence(configuration);
        services.AddSecurity(configuration);
        services.AddStorage(configuration);
        services.AddRealtime(configuration);
        services.AddHostedService<DatabaseSeeder>();
        services.AddHostedService<RefreshTokenCleanupWorker>();
        services.AddSingleton<ISuspensionFinalizerChannel, SuspensionFinalizerChannel>();
        services.AddHostedService<SuspensionFinalizerWorker>();
        services.AddHostedService<QuestionImageCleanupWorker>();
        services.AddHostedService<GameAbandonmentWorker>();
        services.AddSingleton<ISocketEvictionService, SocketEvictionService>();
        services.AddSingleton(TimeProvider.System);

        return services;
    }
}

