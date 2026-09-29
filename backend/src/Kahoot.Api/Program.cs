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
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.HttpOverrides;
using IPNetwork = System.Net.IPNetwork;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Scalar.AspNetCore;

// Application Host Bootstrap - Initializes ASP.NET Core web host builder with configuration and environment providers.
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
{
    // Distributed Tracing & Metrics - Initializes OpenTelemetry, OTLP exporters, and runtime instrumentation.
    builder.AddObservability();

    // MVC Controllers Registration - Configures API controllers and serializes enums as strings.
    builder.Services
        .AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        });
    // OpenAPI Specification - Registers OpenAPI schema generation and Scalar documentation UI.
    builder.Services.AddOpenApi();
    // Global Exception Handling - Registers RFC 7807 problem details handler for unhandled exceptions.
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();

    // Health Probe Registration - Registers DB, Redis, Storage, and composite Readiness health checks.
    builder.Services.AddScoped<DatabaseHealthCheck>();
    builder.Services.AddSingleton<RedisHealthCheck>();
    builder.Services.AddSingleton<StorageHealthCheck>();
    builder.Services.AddSingleton<ReadinessHealthCheck>();
    builder.Services.AddHealthChecks()
        .AddCheck<ReadinessHealthCheck>("readiness", tags: ["ready"]);

    // Reverse Proxy Header Propagation - Trusts forwarded headers from configured CIDR networks for client IP and protocol resolution.
    string[] trustedProxyNetworks = builder.Configuration
        .GetSection("TrustedProxies:Networks").Get<string[]>() ?? [];
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;
        foreach (string configuredNetwork in trustedProxyNetworks)
        {
            if (string.IsNullOrWhiteSpace(configuredNetwork))
            {
                continue;
            }

            if (!IPNetwork.TryParse(configuredNetwork, out IPNetwork network))
            {
                throw new InvalidOperationException("TrustedProxies:Networks contains an invalid CIDR network.");
            }

            options.KnownIPNetworks.Add(network);
        }
    });
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddAuthorization();

    // Clean Architecture Dependency Inversion - Composes application, infrastructure, CORS, JWT, and contextual user services.
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddCorsPolicy(builder.Configuration);
    builder.Services.AddJwtAuthentication(builder.Configuration);
    builder.Services.AddScoped<ICurrentUser, CurrentUser>();
}

// Middleware Pipeline Construction - Builds web application and executes host startup routines.
WebApplication app = builder.Build();
{
    // Asset Storage Staging Provisioning - Ensures local image upload staging directories exist upon service boot.
    string webRoot = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
    Directory.CreateDirectory(Path.Combine(webRoot, "uploads", "staging"));

    // Middleware Pipeline Sequence - Enforces reverse proxy header processing before TLS redirection, routing, and auth.
    app.UseForwardedHeaders();
    app.UseExceptionHandler();
    app.UseHttpsRedirection();
    app.UseRouting();
    app.UseCors("Frontend");
    app.UseAuthentication();
    app.UseAuthorization();

    // Interactive API Documentation - Mounts OpenAPI schema and Scalar interactive reference UI in development mode.
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
    }

    // REST Controllers Mapping - Maps attribute-routed HTTP endpoints.
    app.MapControllers();
    // SignalR Realtime Hub Mapping - Exposes live WebSocket transport hub for host controls and player game interactions.
    app.MapHub<GameHub>("/hubs/game", options =>
    {
        // Transport backpressure - Pauses writes at the 64 KB pipe threshold; blocked sends time out and disconnect.
        options.TransportMaxBufferSize = 64 * 1024;
        options.ApplicationMaxBufferSize = 64 * 1024;
        // Close a connection that cannot drain its bounded send buffer.
        options.TransportSendTimeout = TimeSpan.FromSeconds(2);
        options.CloseOnAuthenticationExpiration = true;
        // Transport Protocol Selection - Restricts realtime communication strictly to WebSockets.
        options.Transports = HttpTransportType.WebSockets;
    });

    // Readiness Health Probe Options - Maps unhealthy or degraded states to 503 Service Unavailable for load balancer traffic cut-off.
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

    // Kubernetes Liveness Probe - Low-overhead endpoint returning 200 OK to confirm process responsiveness without dependency querying.
    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
        Predicate = _ => false
    }).AllowAnonymous();
    // Kubernetes Readiness Probe - Evaluates database, redis, and disk readiness to determine whether replica receives user traffic.
    app.MapHealthChecks("/health/ready", readinessOptions).AllowAnonymous();
    app.MapHealthChecks("/health", readinessOptions).AllowAnonymous();
    // Service Discovery Landing Page - Serves root documentation and status dashboard.
    app.MapHomePage();

    app.Run();
}
