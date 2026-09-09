using FluentValidation;
using Kahoot.Application.Common.Behaviors;
using Mapster;
using Microsoft.Extensions.DependencyInjection;

namespace Kahoot.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssembly(AssemblyReference.Assembly);
            configuration.AddOpenBehavior(typeof(RequestLoggingBehavior<,>));
            configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(AssemblyReference.Assembly, includeInternalTypes: true);

        TypeAdapterConfig.GlobalSettings.Scan(AssemblyReference.Assembly);

        services.AddSingleton(TimeProvider.System);

        return services;
    }
}
