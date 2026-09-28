using Kahoot.Application.Common.Persistence;
using Kahoot.Domain.Enums;
using Kahoot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Kahoot.Infrastructure.ServiceCollectionExtension;

public static class PersistenceInstaller
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetRequiredSection(DatabaseOptions.SectionName))
            .Configure(options => options.ConnectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty)
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString), "ConnectionStrings:DefaultConnection is required.")
            .Validate(options => options.CommandTimeoutSeconds > 0, "Database:CommandTimeoutSeconds must be greater than zero.")
            .Validate(options => options.MigrationCommandTimeoutSeconds > 0, "Database:MigrationCommandTimeoutSeconds must be greater than zero.")
            .ValidateOnStart();

        services.AddSingleton(serviceProvider =>
        {
            DatabaseOptions dbOptions = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;

            NpgsqlDataSourceBuilder dataSourceBuilder = new(dbOptions.ConnectionString)
            {
                Name = "KahootPrimary"
            };

            dataSourceBuilder.MapEnum<UserRole>("user_role");
            dataSourceBuilder.MapEnum<UserStatus>("user_status");
            dataSourceBuilder.MapEnum<GameStatus>("game_status");

            return dataSourceBuilder.Build();
        });

        services.AddScoped<AuditableEntityInterceptor>();

        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            NpgsqlDataSource dataSource = serviceProvider.GetRequiredService<NpgsqlDataSource>();
            DatabaseOptions dbOptions = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;

            options.UseNpgsql(dataSource, npgsqlOptions =>
            {
                npgsqlOptions.MapEnum<UserRole>("user_role");
                npgsqlOptions.MapEnum<UserStatus>("user_status");
                npgsqlOptions.MapEnum<GameStatus>("game_status");
                npgsqlOptions.CommandTimeout(dbOptions.CommandTimeoutSeconds);
            });

            options.UseSnakeCaseNamingConvention();

            AuditableEntityInterceptor auditInterceptor = serviceProvider.GetRequiredService<AuditableEntityInterceptor>();
            options.AddInterceptors(auditInterceptor);

            if (dbOptions.EnableDetailedErrors)
            {
                options.EnableDetailedErrors();
            }

            if (dbOptions.EnableSensitiveDataLogging)
            {
                options.EnableSensitiveDataLogging();
            }
        });

        services.AddScoped<IAppDbContext>(serviceProvider => serviceProvider.GetRequiredService<AppDbContext>());
        services.AddHostedService<DatabaseMigrationService>();

        return services;
    }
}
