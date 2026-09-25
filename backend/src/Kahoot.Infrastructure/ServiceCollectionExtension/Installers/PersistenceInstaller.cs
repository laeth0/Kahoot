using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Persistence;
using Kahoot.Domain.Enums;
using Kahoot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Kahoot.Infrastructure.ServiceCollectionExtension.Installers;

internal sealed class PersistenceInstaller : IServiceInstaller
{
    public IServiceCollection Install(IServiceCollection services, IConfiguration configuration)
    {
        // Register DatabaseOptions from the Database: config section.
        // ConnectionString is wired separately from ConnectionStrings:DefaultConnection
        // so the standard ASP.NET Core convention is preserved and
        // ConnectionStrings__DefaultConnection works as the production environment variable.
        services
            .AddOptions<DatabaseOptions>()
            .Bind(configuration.GetRequiredSection(DatabaseOptions.SectionName))
            .Configure(options =>
            {
                options.ConnectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
            })
            .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString),
                "ConnectionStrings:DefaultConnection is required.")
            .Validate(options => options.CommandTimeoutSeconds > 0,
                "Database:CommandTimeoutSeconds must be greater than zero.")
            .ValidateOnStart();

        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            var dbOptions = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;

            options.UseNpgsql(dbOptions.ConnectionString, npgsqlOptions =>
            {
                npgsqlOptions.MapEnum<UserRole>("user_role");
                npgsqlOptions.MapEnum<UserStatus>("user_status");
                npgsqlOptions.MapEnum<MediaStatus>("media_status");
                npgsqlOptions.MapEnum<GameStatus>("game_status");
                npgsqlOptions.CommandTimeout(dbOptions.CommandTimeoutSeconds);

                // EnableRetryOnFailure is intentionally NOT enabled.
                // All auth operations (Refresh, Logout, LogoutAll, ChangePassword) use
                // explicit BeginTransactionAsync calls. Npgsql's execution retry strategy
                // is incompatible with manual transactions and would throw
                // InvalidOperationException at runtime.
            });

            options.UseSnakeCaseNamingConvention();

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
