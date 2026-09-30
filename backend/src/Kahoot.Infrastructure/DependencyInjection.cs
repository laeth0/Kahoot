using Kahoot.Application.Common.Interfaces;
using Kahoot.Infrastructure.Persistence;
using Kahoot.Infrastructure.Realtime;
using Kahoot.Infrastructure.ServiceCollectionExtension;
using Kahoot.Infrastructure.Services;
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
        string? reconciliationSetting = configuration["DR_RECONCILIATION_ON_STARTUP"];
        bool reconcileOnStartup = false;
        if (reconciliationSetting is not null &&
            !bool.TryParse(reconciliationSetting, out reconcileOnStartup))
        {
            throw new InvalidOperationException("DR_RECONCILIATION_ON_STARTUP must be true or false.");
        }

        if (reconcileOnStartup)
        {
            throw new InvalidOperationException(
                "Disaster recovery admission requires reconciliation against an independently backed-up security ledger before startup.");
        }

        services.AddPersistence(configuration);
        services.AddSecurity(configuration);
        services.AddStorage(configuration);
        services.AddSingleton<ICriticalWorkerFailureTracker, CriticalWorkerFailureTracker>();
        services.AddHostedService<DatabaseSeeder>();
        services.AddRealtime(configuration);
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

