using System.Security.Claims;
using Kahoot.Api.Endpoints;
using Kahoot.Api.HealthChecks;
using Kahoot.Api.Middleware;
using Kahoot.Api.Options;
using Kahoot.Api.Services;
using Kahoot.Application;
using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Persistence;
using Kahoot.Domain.Enums;
using Kahoot.Infrastructure;
using Kahoot.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
const string corsPolicy = "Frontend";

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services
    .AddOptions<CorsOptions>()
    .Bind(builder.Configuration.GetSection(CorsOptions.SectionName));

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database");
builder.Services.AddCors(options => options.AddPolicy(corsPolicy, policy =>
    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
var issuer = jwtSection["Issuer"] ?? string.Empty;
var audience = jwtSection["Audience"] ?? string.Empty;
var signingKey = jwtSection["SigningKey"] ?? string.Empty;
var signingKeyBytes = !string.IsNullOrWhiteSpace(signingKey) ? Convert.FromBase64String(signingKey) : [];

builder.Services
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
            ClockSkew = TimeSpan.Zero
        };

        options.Events = new JwtBearerEvents
        {
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

                var dbContext = context.HttpContext.RequestServices.GetRequiredService<IAppDbContext>();
                var userState = await dbContext.Users
                    .AsNoTracking()
                    .Where(u => u.Id == userId)
                    .Select(u => new { u.Status, u.TokenSecurityVersion })
                    .SingleOrDefaultAsync(context.HttpContext.RequestAborted);

                if (userState is null ||
                    userState.Status != UserStatus.Active ||
                    userState.TokenSecurityVersion != tokenVersion)
                {
                    context.Fail("Token has been revoked or user is not active.");
                    return;
                }
            },
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

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();

app.UseHttpsRedirection();

app.UseRouting();
app.UseCors(corsPolicy);
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();
app.MapHomePage();

app.Run();
