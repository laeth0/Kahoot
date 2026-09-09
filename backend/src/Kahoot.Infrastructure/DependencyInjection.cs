using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Storage;
using Kahoot.Infrastructure.Persistence;
using Kahoot.Infrastructure.Startup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kahoot.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        services.AddDbContext<KahootDbContext>(options =>
            options
                .UseNpgsql(connectionString)
                .UseSnakeCaseNamingConvention());

        services.AddOptions<FileStorageOptions>()
            .Bind(configuration.GetSection(FileStorageOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.Configure<HostSeedOptions>(configuration.GetSection(HostSeedOptions.SectionName));

        services.AddHostedService<DatabaseMigrationHostedService>();
        services.AddHostedService<DatabaseSeederHostedService>();

        services.Scan(scan => scan
            .FromAssemblies(AssemblyReference.Assembly, Application.AssemblyReference.Assembly)
            .AddClasses(classes => classes.AssignableTo<ITransientService>())
                .AsImplementedInterfaces()
                .WithTransientLifetime()
            .AddClasses(classes => classes.AssignableTo<IScopedService>())
                .AsImplementedInterfaces()
                .WithScopedLifetime()
            .AddClasses(classes => classes.AssignableTo<ISingletonService>())
                .AsImplementedInterfaces()
                .WithSingletonLifetime());

        return services;
    }
}
