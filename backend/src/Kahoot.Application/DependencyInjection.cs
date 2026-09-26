using FluentValidation;
using Kahoot.Application.Common.Behaviors;
using Kahoot.Application.Common.Seeding;
using Kahoot.Application.Features.Auth.Bootstrap;
using Mapster;
using Microsoft.Extensions.DependencyInjection;

namespace Kahoot.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(AssemblyReference.Assembly);
            config.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(AssemblyReference.Assembly);

        TypeAdapterConfig.GlobalSettings.Scan(AssemblyReference.Assembly);

        services.AddScoped<ISeeder, SystemAdminSeeder>();

        return services;
    }
}
