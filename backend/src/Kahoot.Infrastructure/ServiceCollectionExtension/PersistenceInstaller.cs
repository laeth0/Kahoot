using Kahoot.Application.Common.Persistence;
using Kahoot.Domain.Enums;
using Kahoot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Kahoot.Infrastructure.ServiceCollectionExtension;

// Persistence Infrastructure Installer - Configures PostgreSQL connection pooling, enum mappings, snake_case conventions, and EF Core interceptors.
public static class PersistenceInstaller
{
    // Persistence Registration Pipeline - Binds database options, registers NpgsqlDataSource with custom PostgreSQL enums, and configures AppDbContext.
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetRequiredSection(DatabaseOptions.SectionName))
            .Configure(options => options.ConnectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty)
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString), "ConnectionStrings:DefaultConnection is required.")
            .Validate(options => options.CommandTimeoutSeconds > 0, "Database:CommandTimeoutSeconds must be greater than zero.")
            .Validate(options => options.MigrationCommandTimeoutSeconds > 0, "Database:MigrationCommandTimeoutSeconds must be greater than zero.")
            .ValidateOnStart();

        // Optimized Npgsql Multi-Host Data Source - Builds reusable NpgsqlDataSource mapping PostgreSQL native enum types with explicit connection pool bounds (ARCH-POOL-001, ARCH-POOL-002).
        services.AddSingleton(serviceProvider =>
        {
            DatabaseOptions dbOptions = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;

            NpgsqlConnectionStringBuilder connStrBuilder = new(dbOptions.ConnectionString);
            // Bounded Pool Acquisition Timeout (ARCH-POOL-002) - Sets maximum 15-second connection acquisition timeout
            if (connStrBuilder.Timeout <= 0 || connStrBuilder.Timeout > 15)
            {
                connStrBuilder.Timeout = 15;
            }

            // Connection Pool Boundaries (ARCH-POOL-001) - Enforces 10 minimum and 80 maximum pool connections for single backend replica
            if (!connStrBuilder.ContainsKey("Maximum Pool Size"))
            {
                connStrBuilder.MaxPoolSize = 80;
            }

            if (!connStrBuilder.ContainsKey("Minimum Pool Size"))
            {
                connStrBuilder.MinPoolSize = 10;
            }

            NpgsqlDataSourceBuilder dataSourceBuilder = new(connStrBuilder.ConnectionString)
            {
                Name = "KahootPrimary"
            };

            dataSourceBuilder.MapEnum<UserRole>("user_role");
            dataSourceBuilder.MapEnum<UserStatus>("user_status");
            dataSourceBuilder.MapEnum<GameStatus>("game_status");

            return dataSourceBuilder.Build();
        });

        services.AddScoped<AuditableEntityInterceptor>();

        // DbContext Connection Pooling & Configuration - Applies snake_case naming conventions and audit interceptors to EF Core.
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
