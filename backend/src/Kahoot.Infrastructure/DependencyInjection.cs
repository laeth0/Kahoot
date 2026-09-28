using Kahoot.Application.Common.Interfaces;
using Kahoot.Infrastructure.Persistence;
using Kahoot.Infrastructure.Realtime;
using Kahoot.Infrastructure.ServiceCollectionExtension;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kahoot.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddPersistence(configuration);
        services.AddSecurity(configuration);
        services.AddStorage(configuration);
        services.AddHostedService<DatabaseSeeder>();
        services.AddHostedService<RefreshTokenCleanupWorker>();
        services.AddSingleton<ISuspensionFinalizerChannel, SuspensionFinalizerChannel>();
        services.AddHostedService<SuspensionFinalizerWorker>();
        services.AddHostedService<QuestionImageCleanupWorker>();
        services.AddSingleton<ISocketEvictionService, SocketEvictionService>();
        services.AddSingleton(TimeProvider.System);

        return services;
    }
}

