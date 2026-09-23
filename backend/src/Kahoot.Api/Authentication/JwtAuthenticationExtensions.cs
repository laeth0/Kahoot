using Kahoot.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Kahoot.Api.Authentication;

internal static class JwtAuthenticationExtensions
{
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(JwtOptions.IsValid, "Jwt settings require an issuer, audience, positive lifetimes, and a 64-character hexadecimal signing key.")
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearerOptions, configuredOptions) =>
            {
                var jwt = configuredOptions.Value;
                bearerOptions.MapInboundClaims = false;
                bearerOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    ValidateIssuerSigningKey = true,
                    RequireSignedTokens = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Convert.FromHexString(jwt.SigningKey)),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ClockSkew = TimeSpan.FromMinutes(1)
                };

                bearerOptions.Events = new JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.Headers["WWW-Authenticate"] = "Bearer";
                        await Results.Problem(
                            statusCode: StatusCodes.Status401Unauthorized,
                            title: "Unauthorized").ExecuteAsync(context.HttpContext);
                    },
                    OnForbidden = async context =>
                    {
                        await Results.Problem(
                            statusCode: StatusCodes.Status403Forbidden,
                            title: "Forbidden").ExecuteAsync(context.HttpContext);
                    }
                };
            });

        services.AddAuthorization();
        return services;
    }
}
