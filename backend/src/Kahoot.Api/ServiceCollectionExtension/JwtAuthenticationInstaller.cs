using System.Security.Claims;
using Kahoot.Application.Common.Persistence;
using Kahoot.Domain.Enums;
using Kahoot.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Kahoot.Api.ServiceCollectionExtension;

public static class JwtAuthenticationInstaller
{
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        IConfigurationSection jwtSection = configuration.GetSection(JwtOptions.SectionName);
        string issuer = jwtSection["Issuer"] ?? string.Empty;
        string audience = jwtSection["Audience"] ?? string.Empty;
        string signingKey = jwtSection["SigningKey"] ?? string.Empty;
        byte[] signingKeyBytes = !string.IsNullOrWhiteSpace(signingKey)
            ? Convert.FromBase64String(signingKey)
            : [];

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(signingKeyBytes),
                    ValidateLifetime = true,
                    // No clock skew: tokens expire exactly at Exp to keep the 15-minute access-token
                    // lifetime tight and consistent with the security model.
                    ClockSkew = TimeSpan.Zero
                };

                options.Events = new JwtBearerEvents
                {
                    // After signature/lifetime validation succeeds, verify that the token's
                    // token_security_version still matches the current value in the database.
                    // This is the mechanism that makes logout-all and account suspension take
                    // effect for access tokens that are structurally still valid.
                    OnTokenValidated = async context =>
                    {
                        string? sub = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                            ?? context.Principal?.FindFirstValue("sub");

                        if (!Guid.TryParse(sub, out Guid userId))
                        {
                            context.Fail("Invalid user identifier in token.");
                            return;
                        }

                        string? versionClaim = context.Principal?.FindFirstValue("token_security_version");
                        if (!int.TryParse(versionClaim, out int tokenVersion))
                        {
                            context.Fail("Missing or invalid token_security_version claim.");
                            return;
                        }

                        IAppDbContext dbContext = context.HttpContext.RequestServices
                            .GetRequiredService<IAppDbContext>();

                        UserSecurityState? userState = await dbContext.Users
                            .AsNoTracking()
                            .Where(u => u.Id == userId)
                            .Select(u => new UserSecurityState(u.Status, u.Role, u.TokenSecurityVersion))
                            .SingleOrDefaultAsync(context.HttpContext.RequestAborted);

                        if (userState is null
                            || userState.Status != UserStatus.Active
                            || userState.TokenSecurityVersion != tokenVersion
                            || !string.Equals(
                                userState.Role.ToString(),
                                context.Principal?.FindFirstValue(ClaimTypes.Role) ?? context.Principal?.FindFirstValue("role"),
                                StringComparison.Ordinal))
                        {
                            context.Fail("Token has been revoked or user is not active.");
                        }
                    },

                    // Produce a consistent RFC7807 ProblemDetails 401 instead of the default
                    // WWW-Authenticate challenge so clients receive the same error shape as
                    // all other API errors.
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/problem+json";

                        IResult problem = Results.Problem(
                            statusCode: StatusCodes.Status401Unauthorized,
                            title: "Unauthorized",
                            detail: "Authentication is required to access this resource, or token is invalid.",
                            instance: context.HttpContext.Request.Path,
                            type: "https://api.kahoot-saas.local/errors/Auth.Unauthorized",
                            extensions: new Dictionary<string, object?>
                            {
                                ["code"] = "Auth.Unauthorized",
                                ["requestId"] = context.HttpContext.TraceIdentifier
                            });

                        await problem.ExecuteAsync(context.HttpContext);
                    },

                    OnForbidden = async context =>
                    {
                        await Results.Problem(
                            statusCode: StatusCodes.Status403Forbidden,
                            title: "Forbidden",
                            detail: "The authenticated account is not allowed to access this resource.",
                            instance: context.HttpContext.Request.Path,
                            type: "https://api.kahoot-saas.local/errors/Auth.Forbidden",
                            extensions: new Dictionary<string, object?>
                            {
                                ["code"] = "Auth.Forbidden",
                                ["requestId"] = context.HttpContext.TraceIdentifier
                            })
                            .ExecuteAsync(context.HttpContext);
                    }
                };
            });

        return services;
    }

    private sealed record UserSecurityState(UserStatus Status, UserRole Role, int TokenSecurityVersion);
}
