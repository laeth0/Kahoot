using Kahoot.Application.Common.Persistence;
using Kahoot.Domain.Enums;
using Kahoot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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

        services.AddScoped<AuditableEntityInterceptor>();

        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            DatabaseOptions dbOptions = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;

            options.UseNpgsql(dbOptions.ConnectionString, npgsqlOptions =>
            {
                npgsqlOptions.MapEnum<UserRole>("user_role");
                npgsqlOptions.MapEnum<UserStatus>("user_status");
                npgsqlOptions.MapEnum<MediaStatus>("media_status");
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
