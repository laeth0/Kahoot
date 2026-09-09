using FluentValidation;
using Mapster;
using Microsoft.Extensions.DependencyInjection;

namespace Kahoot.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(AssemblyReference.Assembly);
        });

        services.AddValidatorsFromAssembly(AssemblyReference.Assembly);

        TypeAdapterConfig.GlobalSettings.Scan(AssemblyReference.Assembly);

        return services;
    }
}
