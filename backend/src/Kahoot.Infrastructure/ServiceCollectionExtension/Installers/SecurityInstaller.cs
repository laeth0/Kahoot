using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Options;
using Kahoot.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kahoot.Infrastructure.ServiceCollectionExtension.Installers;

internal sealed class SecurityInstaller : IServiceInstaller
{
    public IServiceCollection Install(IServiceCollection services, IConfiguration configuration)
    {
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
