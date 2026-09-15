using System.Text;
using Kahoot.Application.Authentication.Common;
using Kahoot.Application.Common.Storage;
using Kahoot.Infrastructure.Options;

namespace Kahoot.Api.Common;

internal static class ConfigurationValidator
{
    private static readonly string[] InsecureSigningKeyPlaceholders =
    [
        "replace_with_secure_random_key_min_32_chars_long_123456789",
        "replace_with_strong_password_here",
        "your_secret_key_here",
        "secret_key_needs_to_be_at_least_32_characters_long"
    ];

    public static void Validate(IConfiguration configuration, IWebHostEnvironment environment)
    {
        string? connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Configuration validation failed: Connection string 'DefaultConnection' is missing or empty.");
        }

        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>();
        if (jwtOptions is null)
        {
            throw new InvalidOperationException("Configuration validation failed: 'Jwt' configuration section is missing.");
        }

        if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey))
        {
            throw new InvalidOperationException("Configuration validation failed: 'Jwt:SigningKey' is missing or empty.");
        }

        if (Encoding.UTF8.GetByteCount(jwtOptions.SigningKey) < 32)
        {
            throw new InvalidOperationException("Configuration validation failed: 'Jwt:SigningKey' must be at least 32 bytes (256 bits).");
        }

        if (!environment.IsDevelopment())
        {
            foreach (string placeholder in InsecureSigningKeyPlaceholders)
            {
                if (jwtOptions.SigningKey.Contains(placeholder, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Configuration validation failed: Insecure placeholder detected in 'Jwt:SigningKey' in a non-development environment.");
                }
            }
        }

        if (string.IsNullOrWhiteSpace(jwtOptions.Issuer))
        {
            throw new InvalidOperationException("Configuration validation failed: 'Jwt:Issuer' is missing or empty.");
        }

        if (string.IsNullOrWhiteSpace(jwtOptions.Audience))
        {
            throw new InvalidOperationException("Configuration validation failed: 'Jwt:Audience' is missing or empty.");
        }

        string[] configuredOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? configuration["Cors:AllowedOrigins"]?.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ?? [];

        if (!environment.IsDevelopment())
        {
            if (configuredOrigins.Length == 0)
            {
                throw new InvalidOperationException("Configuration validation failed: 'Cors:AllowedOrigins' must be configured in non-development environments.");
            }

            foreach (string origin in configuredOrigins)
            {
                if (!Uri.TryCreate(origin, UriKind.Absolute, out Uri? uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    throw new InvalidOperationException($"Configuration validation failed: Invalid CORS origin '{origin}'. Must be an absolute HTTP or HTTPS URI.");
                }
            }
        }

        string? clientBaseUrl = configuration.GetSection(ClientOptions.SectionName).Get<ClientOptions>()?.BaseUrl
            ?? configuration["Client:BaseUrl"];
        if (!string.IsNullOrWhiteSpace(clientBaseUrl))
        {
            if (!Uri.TryCreate(clientBaseUrl, UriKind.Absolute, out Uri? uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException($"Configuration validation failed: Invalid 'Client:BaseUrl' '{clientBaseUrl}'. Must be an absolute HTTP or HTTPS URI.");
            }
        }

        var storageOptions = configuration.GetSection(FileStorageOptions.SectionName).Get<FileStorageOptions>()
            ?? new FileStorageOptions();

        if (string.IsNullOrWhiteSpace(storageOptions.RootPath))
        {
            throw new InvalidOperationException("Configuration validation failed: 'FileStorage:RootPath' cannot be empty.");
        }

        if (storageOptions.MaxSizeBytes <= 0)
        {
            throw new InvalidOperationException("Configuration validation failed: 'FileStorage:MaxSizeBytes' must be greater than zero.");
        }

        string? allowedHosts = configuration["AllowedHosts"];
        if (string.IsNullOrWhiteSpace(allowedHosts))
        {
            throw new InvalidOperationException("Configuration validation failed: 'AllowedHosts' is missing or empty.");
        }
    }
}
