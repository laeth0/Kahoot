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

namespace Kahoot.Api.ServiceCollectionExtension.Installers;

internal sealed class JwtAuthenticationInstaller : IServiceInstaller
{
    public IServiceCollection Install(IServiceCollection services, IConfiguration configuration)
    {
        var jwtSection = configuration.GetSection(JwtOptions.SectionName);
        var issuer = jwtSection["Issuer"] ?? string.Empty;
        var audience = jwtSection["Audience"] ?? string.Empty;
        var signingKey = jwtSection["SigningKey"] ?? string.Empty;
        var signingKeyBytes = !string.IsNullOrWhiteSpace(signingKey)
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
                        var sub = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                            ?? context.Principal?.FindFirstValue("sub");

                        if (!Guid.TryParse(sub, out var userId))
                        {
                            context.Fail("Invalid user identifier in token.");
                            return;
                        }

                        var versionClaim = context.Principal?.FindFirstValue("token_security_version");
                        if (!int.TryParse(versionClaim, out var tokenVersion))
                        {
                            context.Fail("Missing or invalid token_security_version claim.");
                            return;
                        }

                        var dbContext = context.HttpContext.RequestServices
                            .GetRequiredService<IAppDbContext>();

                        var userState = await dbContext.Users
                            .AsNoTracking()
                            .Where(u => u.Id == userId)
                            .Select(u => new { u.Status, u.TokenSecurityVersion })
                            .SingleOrDefaultAsync(context.HttpContext.RequestAborted);

                        if (userState is null
                            || userState.Status != UserStatus.Active
                            || userState.TokenSecurityVersion != tokenVersion)
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

                        var problem = Results.Problem(
                            statusCode: StatusCodes.Status401Unauthorized,
                            title: "Auth.Unauthorized",
                            detail: "Authentication is required to access this resource, or token is invalid.",
                            extensions: new Dictionary<string, object?> { ["code"] = "Auth.Unauthorized" });

                        await problem.ExecuteAsync(context.HttpContext);
                    }
                };
            });

        return services;
    }
}
