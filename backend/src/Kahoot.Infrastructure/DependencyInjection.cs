using Kahoot.Application.Authentication.Common;
using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Storage;
using Kahoot.Infrastructure.Authentication;
using Kahoot.Infrastructure.Options;
using Kahoot.Infrastructure.Persistence;
using Kahoot.Infrastructure.Startup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Kahoot.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        NpgsqlDataSource dataSource = new NpgsqlDataSourceBuilder(connectionString)
        {
            Name = "kahoot-db"
        }.Build();

        services.AddSingleton(dataSource);

        services.AddDbContext<KahootDbContext>(options =>
            options
                .UseNpgsql(dataSource)
                .UseSnakeCaseNamingConvention());

        services.AddScoped<IApplicationDbContext>(serviceProvider =>
            serviceProvider.GetRequiredService<KahootDbContext>());

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<FileStorageOptions>()
            .Bind(configuration.GetSection(FileStorageOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.Configure<HostSeedOptions>(configuration.GetSection(HostSeedOptions.SectionName));
        services.Configure<ClientOptions>(configuration.GetSection(ClientOptions.SectionName));

        services.AddHostedService<DatabaseMigrationHostedService>();
        services.AddHostedService<DatabaseSeederHostedService>();
        services.AddHostedService<TokenCleanupHostedService>();

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
