using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Options;
using Kahoot.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kahoot.Infrastructure.ServiceCollectionExtension;

public static class SecurityInstaller
{
    public static IServiceCollection AddSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IPasswordHasher, PasswordHasher>();

        services
            .AddOptions<BootstrapAdminOptions>()
            .Bind(configuration.GetRequiredSection(BootstrapAdminOptions.SectionName))
            .Configure(options =>
            {
                string? enabledValue = configuration["BOOTSTRAP_ADMIN_ENABLED"];
                if (enabledValue is not null)
                {
                    if (!bool.TryParse(enabledValue, out bool enabled))
                    {
                        throw new InvalidOperationException("BOOTSTRAP_ADMIN_ENABLED must be true or false.");
                    }

                    options.Enabled = enabled;
                }

                if (configuration["BOOTSTRAP_ADMIN_USERNAME"] is { } username)
                {
                    options.Username = username;
                }

                if (configuration["BOOTSTRAP_ADMIN_PASSWORD"] is { } password)
                {
                    options.Password = password;
                }
            })
            .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(options.Username),
                "BOOTSTRAP_ADMIN_USERNAME is required when bootstrap is enabled.")
            .Validate(options => !options.Enabled || options.Password.Length is >= 16 and <= 128,
                "BOOTSTRAP_ADMIN_PASSWORD must be 16 to 128 characters when bootstrap is enabled.")
            .ValidateOnStart();

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
