using Kahoot.Api.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kahoot.Api.ServiceCollectionExtension;

// CORS Installer - Configures browser origin security policies, credential propagation, and preflight handling.
public static class CorsInstaller
{
    // CORS Service Registration - Binds allowed frontend domains and registers named CORS policy permitting credentials.
    public static IServiceCollection AddCorsPolicy(this IServiceCollection services, IConfiguration configuration)
    {
        string[] allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        services
            .AddOptions<CorsOptions>()
            .Bind(configuration.GetSection(CorsOptions.SectionName));

        // Frontend Policy Configuration - Allows specific origins with full HTTP verbs, custom headers, and cookie/credential sharing.
        services.AddCors(options => options.AddPolicy(
            "Frontend",
            policy => policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

        return services;
    }
}
