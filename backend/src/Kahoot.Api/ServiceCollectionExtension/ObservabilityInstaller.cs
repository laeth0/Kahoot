using System.Diagnostics;
using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Kahoot.Api.ServiceCollectionExtension;

public static class ObservabilityInstaller
{
    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {
        builder.Services.AddObservability(builder.Configuration, builder.Environment, builder.Logging);
        return builder;
    }

    public static IServiceCollection AddObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILoggingBuilder logging)
    {
        string? configuredServiceName = configuration["OTEL_SERVICE_NAME"];
        string serviceName = string.IsNullOrWhiteSpace(configuredServiceName)
            ? "Kahoot.Api"
            : configuredServiceName.Trim();
        string serviceVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0";
        string instanceId = Guid.NewGuid().ToString("N");

        Action<ResourceBuilder> configureResource = resource =>
        {
            resource
                .AddService(
                    serviceName: serviceName,
                    serviceNamespace: "Kahoot",
                    serviceVersion: serviceVersion,
                    serviceInstanceId: instanceId)
                .AddAttributes([
                    new KeyValuePair<string, object>("deployment.environment.name", environment.EnvironmentName.ToLowerInvariant())
                ]);
        };

        Sampler traceSampler = ResolveSampler(configuration, environment);

        services.AddOpenTelemetry()
            .ConfigureResource(configureResource)
            .WithTracing(tracing =>
            {
                tracing
                    .SetSampler(traceSampler)
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.Filter = httpContext =>
                        {
                            PathString path = httpContext.Request.Path;
                            return !path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase);
                        };
                        options.EnrichWithHttpRequest = (activity, _) => RemoveInboundRequestTags(activity);
                        options.EnrichWithHttpResponse = (activity, _) => RemoveInboundRequestTags(activity);
                        options.EnrichWithException = (activity, _) => RemoveInboundRequestTags(activity);
                    })
                    .AddHttpClientInstrumentation(options =>
                    {
                        options.EnrichWithHttpRequestMessage = (activity, _) => RemoveOutboundUrlTag(activity);
                        options.EnrichWithHttpResponseMessage = (activity, _) => RemoveOutboundUrlTag(activity);
                        options.EnrichWithException = (activity, _) => RemoveOutboundUrlTag(activity);
                    })
                    .AddNpgsql()
                    .AddOtlpExporter();
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter("Npgsql")
                    .AddOtlpExporter();
            });

        logging.AddOpenTelemetry(options =>
        {
            ResourceBuilder logResource = ResourceBuilder.CreateDefault();
            configureResource(logResource);
            options.SetResourceBuilder(logResource);
            options.IncludeFormattedMessage = true;
            options.IncludeScopes = true;
            options.AddOtlpExporter();
        });

        return services;
    }

    private static void RemoveInboundRequestTags(Activity activity)
    {
        // Route templates remain available; raw paths and host headers can contain user-controlled values.
        activity.SetTag("url.path", null);
        activity.SetTag("url.query", null);
        activity.SetTag("user_agent.original", null);
        activity.SetTag("server.address", null);
        activity.SetTag("http.request.method_original", null);
    }

    private static void RemoveOutboundUrlTag(Activity activity)
    {
        activity.SetTag("url.full", null);
    }

    private static Sampler ResolveSampler(IConfiguration configuration, IHostEnvironment environment)
    {
        string? samplerType = configuration["OTEL_TRACES_SAMPLER"]?.Trim().ToLowerInvariant();
        string? samplerArg = configuration["OTEL_TRACES_SAMPLER_ARG"]?.Trim();

        double ratio = 0.10;
        if (!string.IsNullOrEmpty(samplerArg))
        {
            if (!double.TryParse(samplerArg, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedRatio) ||
                !double.IsFinite(parsedRatio) ||
                parsedRatio is < 0.0 or > 1.0)
            {
                throw new InvalidOperationException("OTEL_TRACES_SAMPLER_ARG must be a number from 0 to 1.");
            }

            ratio = parsedRatio;
        }

        if (string.IsNullOrEmpty(samplerType))
        {
            return environment.IsDevelopment()
                ? new ParentBasedSampler(new AlwaysOnSampler())
                : new ParentBasedSampler(new TraceIdRatioBasedSampler(ratio));
        }

        return samplerType switch
        {
            "always_on" => new AlwaysOnSampler(),
            "always_off" => new AlwaysOffSampler(),
            "traceidratio" => new TraceIdRatioBasedSampler(ratio),
            "parentbased_always_on" => new ParentBasedSampler(new AlwaysOnSampler()),
            "parentbased_always_off" => new ParentBasedSampler(new AlwaysOffSampler()),
            "parentbased_traceidratio" => new ParentBasedSampler(new TraceIdRatioBasedSampler(ratio)),
            _ => throw new InvalidOperationException("OTEL_TRACES_SAMPLER is not a supported sampler.")
        };
    }
}
