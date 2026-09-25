using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Options;
using Kahoot.Application.Common.Persistence;
using Kahoot.Domain.Enums;
using Kahoot.Infrastructure.Persistence;
using Kahoot.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Kahoot.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
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
                // Populate ConnectionString from the standard connection string convention,
                // not from the Database section. This keeps the two concerns separated.
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
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IPasswordHasher, PasswordHasher>();

        services
            .AddOptions<JwtOptions>()
            .Bind(configuration.GetRequiredSection(JwtOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Issuer), "Jwt:Issuer is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Audience), "Jwt:Audience is required.")
            .Validate(
                options => IsValidBase64Key(options.SigningKey, 32),
                "Jwt:SigningKey must be a valid Base64 string representing at least 32 bytes (256 bits).")
            .Validate(options => options.AccessTokenMinutes > 0, "Jwt:AccessTokenMinutes must be greater than zero.")
            .ValidateOnStart();

        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddSingleton<ILoginRateLimiter, LoginRateLimiter>();

        services
            .AddOptions<RefreshTokenOptions>()
            .Bind(configuration.GetRequiredSection(RefreshTokenOptions.SectionName))
            .Validate(options => options.LifetimeDays > 0, "RefreshToken:LifetimeDays must be greater than zero.")
            .Validate(
                options => options.FamilyMaxLifetimeDays >= options.LifetimeDays,
                "RefreshToken:FamilyMaxLifetimeDays must be greater than or equal to LifetimeDays.")
            .ValidateOnStart();

        return services;
    }

    private static bool IsValidBase64Key(string? signingKey, int minByteLength)
    {
        if (string.IsNullOrWhiteSpace(signingKey))
        {
            return false;
        }

        try
        {
            var bytes = Convert.FromBase64String(signingKey);
            return bytes.Length >= minByteLength;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
