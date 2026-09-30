using System.Security.Claims;
using Kahoot.Application.Common.Persistence;
using Kahoot.Domain.Enums;
using Kahoot.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Tokens;

namespace Kahoot.Api.ServiceCollectionExtension;

// JWT Authentication Installer - Configures Bearer token validation, SignalR query string token extraction, and real-time revocation checks.
public static class JwtAuthenticationInstaller
{
    // JWT Bearer Registration - Binds token validation parameters, zero clock skew, and authentication event hooks.
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
                // Claim Type Preservation - Disables default WS-Federation XML schema claim mapping to preserve standard OIDC JWT claim keys.
                options.MapInboundClaims = false;
                // Strict Token Validation - Enforces issuer, audience, symmetric cryptographic signature, and exact lifetime expiration.
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(signingKeyBytes),
                    ValidateLifetime = true,
                    RoleClaimType = "role",
                    // Zero Clock Skew - Eliminates default 5-minute leeway to ensure strict 15-minute access token expiration.
                    ClockSkew = TimeSpan.Zero
                };

                options.Events = new JwtBearerEvents
                {
                    // WebSocket Query Token Extraction - Extracts Bearer access_token from HttpContext.Items (redacted) or query string for browser SignalR connections.
                    OnMessageReceived = context =>
                    {
                        if (context.Request.Path.StartsWithSegments("/hubs/game"))
                        {
                            if (context.HttpContext.Items.TryGetValue("access_token", out object? tokenObj) &&
                                tokenObj is string token &&
                                !string.IsNullOrEmpty(token))
                            {
                                context.Token = token;
                            }
                            else if (context.Request.Query.TryGetValue("access_token", out StringValues accessToken) &&
                                     !StringValues.IsNullOrEmpty(accessToken) &&
                                     !string.Equals(accessToken.ToString(), "[REDACTED]", StringComparison.Ordinal))
                            {
                                context.Token = accessToken;
                            }
                        }

                        return Task.CompletedTask;
                    },
                    // Security Version Revocation Check - Queries database to verify user status, role, and TokenSecurityVersion match active credentials.
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

                    // RFC 7807 Unauthorized Challenge - Renders standardized ProblemDetails 401 response with trace correlation identifiers.
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/problem+json";

                        Dictionary<string, object?> extensions = new()
                        {
                            ["code"] = "Auth.Unauthorized",
                            ["requestId"] = context.HttpContext.TraceIdentifier
                        };

                        if (System.Diagnostics.Activity.Current is not null)
                        {
                            extensions["traceId"] = System.Diagnostics.Activity.Current.TraceId.ToString();
                        }

                        IResult problem = Results.Problem(
                            statusCode: StatusCodes.Status401Unauthorized,
                            title: "Unauthorized",
                            detail: "Authentication is required to access this resource, or token is invalid.",
                            instance: context.HttpContext.Request.Path,
                            type: "https://api.kahoot-saas.local/errors/Auth.Unauthorized",
                            extensions: extensions);

                        await problem.ExecuteAsync(context.HttpContext);
                    },

                    // RFC 7807 Forbidden Event - Renders standardized ProblemDetails 403 response when principal lacks required role permissions.
                    OnForbidden = async context =>
                    {
                        Dictionary<string, object?> extensions = new()
                        {
                            ["code"] = "Auth.Forbidden",
                            ["requestId"] = context.HttpContext.TraceIdentifier
                        };

                        if (System.Diagnostics.Activity.Current is not null)
                        {
                            extensions["traceId"] = System.Diagnostics.Activity.Current.TraceId.ToString();
                        }

                        await Results.Problem(
                            statusCode: StatusCodes.Status403Forbidden,
                            title: "Forbidden",
                            detail: "The authenticated account is not allowed to access this resource.",
                            instance: context.HttpContext.Request.Path,
                            type: "https://api.kahoot-saas.local/errors/Auth.Forbidden",
                            extensions: extensions)
                            .ExecuteAsync(context.HttpContext);
                    }
                };
            });

        return services;
    }

    // User Security State Projection - Minimal DTO projecting user status, role, and security version for token validation.
    private sealed record UserSecurityState(UserStatus Status, UserRole Role, int TokenSecurityVersion);
}
