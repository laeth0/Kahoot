using Kahoot.Application.Common.Interfaces;
using Kahoot.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kahoot.Infrastructure.ServiceCollectionExtension;

internal sealed class RefreshTokenCleanupInstaller : IServiceInstaller
{
    public IServiceCollection Install(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHostedService<RefreshTokenCleanupWorker>();

        return services;
    }
}
