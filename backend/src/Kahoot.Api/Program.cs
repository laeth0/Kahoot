using Kahoot.Api;
using Kahoot.Api.Endpoints;
using Kahoot.Api.HealthChecks;
using Kahoot.Api.Middleware;
using Kahoot.Api.ServiceCollectionExtension;
using Kahoot.Api.Services;
using Kahoot.Application;
using Kahoot.Application.Common.Interfaces;
using Kahoot.Infrastructure;
using Kahoot.Infrastructure.Realtime;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Scalar.AspNetCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
{
    builder.AddObservability();

    builder.Services
        .AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        });
    builder.Services.AddOpenApi();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();
    builder.Services.AddScoped<DatabaseHealthCheck>();
    builder.Services.AddSingleton<RedisHealthCheck>();
    builder.Services.AddSingleton<StorageHealthCheck>();
    builder.Services.AddSingleton<ReadinessHealthCheck>();
    builder.Services.AddHealthChecks()
        .AddCheck<ReadinessHealthCheck>("readiness", tags: ["ready"]);
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddAuthorization();

    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddCorsPolicy(builder.Configuration);
    builder.Services.AddJwtAuthentication(builder.Configuration);
    builder.Services.AddScoped<ICurrentUser, CurrentUser>();
}


WebApplication app = builder.Build();
{
    string webRoot = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
    Directory.CreateDirectory(Path.Combine(webRoot, "uploads", "staging"));

    app.UseExceptionHandler();
    app.UseHttpsRedirection();
    app.UseRouting();
    app.UseCors("Frontend");
    app.UseAuthentication();
    app.UseAuthorization();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
    }

    app.MapControllers();
    app.MapHub<GameHub>("/hubs/game");
    HealthCheckOptions readinessOptions = new()
    {
        Predicate = registration => registration.Tags.Contains("ready"),
        ResultStatusCodes =
        {
            [HealthStatus.Healthy] = StatusCodes.Status200OK,
            [HealthStatus.Degraded] = StatusCodes.Status503ServiceUnavailable,
            [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable
        }
    };

    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = _ => false
    }).AllowAnonymous();
    app.MapHealthChecks("/health/ready", readinessOptions).AllowAnonymous();
    app.MapHealthChecks("/health", readinessOptions).AllowAnonymous();
    app.MapHomePage();

    app.Run();
}
