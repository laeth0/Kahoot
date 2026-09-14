using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Kahoot.Api.Observability;

public static class ObservabilityExtensions
{
    public static IHostApplicationBuilder AddKahootObservability(this IHostApplicationBuilder builder)
    {
        var options = new ObservabilityOptions();
        builder.Configuration.GetSection(ObservabilityOptions.SectionName).Bind(options);

        var validationContext = new ValidationContext(options);
        var validationResults = new List<ValidationResult>();
        if (!Validator.TryValidateObject(options, validationContext, validationResults, validateAllProperties: true))
        {
            var failureMessages = validationResults
                .Select(vr => vr.ErrorMessage ?? "Validation error occurred.")
                .ToArray();

            throw new OptionsValidationException(
                ObservabilityOptions.SectionName,
                typeof(ObservabilityOptions),
                failureMessages);
        }

        builder.Services.AddOptions<ObservabilityOptions>()
            .Bind(builder.Configuration.GetSection(ObservabilityOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var serviceVersion = typeof(ObservabilityExtensions).Assembly.GetName().Version?.ToString() ?? "1.0.0";

        var resourceBuilder = ResourceBuilder.CreateDefault()
            .AddService(options.ServiceName, serviceVersion: serviceVersion)
            .AddAttributes(new Dictionary<string, object>
            {
                ["deployment.environment.name"] = builder.Environment.EnvironmentName
            });

        builder.Logging.AddJsonConsole(consoleOptions =>
        {
            consoleOptions.IncludeScopes = true;
            consoleOptions.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ ";
            consoleOptions.UseUtcTimestamp = true;
        });

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.SetResourceBuilder(resourceBuilder);
            logging.IncludeScopes = true;
            logging.IncludeFormattedMessage = true;
            logging.ParseStateValues = true;

            if (options.Enabled && options.OtlpEndpoint is not null)
            {
                logging.AddOtlpExporter(exporter =>
                {
                    exporter.Endpoint = options.OtlpEndpoint;
                });
            }
        });

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(options.ServiceName, serviceVersion: serviceVersion)
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment.name"] = builder.Environment.EnvironmentName
                }))
            .WithTracing(tracing =>
            {
                tracing
                    .SetSampler(new AlwaysOnSampler())
                    .AddSource(
                        ObservabilityNames.ActivitySourceName,
                        "Microsoft.AspNetCore.SignalR.Server")
                    .AddAspNetCoreInstrumentation(instrumentation =>
                    {
                        instrumentation.RecordException = true;
                        instrumentation.Filter = context => context.Request.Path != "/health";
                    })
                    .AddNpgsql();

                if (options.Enabled && options.OtlpEndpoint is not null)
                {
                    tracing.AddOtlpExporter(exporter =>
                    {
                        exporter.Endpoint = options.OtlpEndpoint;
                    });
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .AddMeter(
                        ObservabilityNames.MeterName,
                        "Microsoft.AspNetCore.Hosting",
                        "Microsoft.AspNetCore.Server.Kestrel",
                        "Microsoft.AspNetCore.Http.Connections",
                        "Microsoft.AspNetCore.RateLimiting",
                        "Microsoft.AspNetCore.Diagnostics",
                        "Npgsql")
                    .AddRuntimeInstrumentation();

                if (options.Enabled && options.OtlpEndpoint is not null)
                {
                    metrics.AddOtlpExporter(exporter =>
                    {
                        exporter.Endpoint = options.OtlpEndpoint;
                    });
                }
            });

        return builder;
    }
}
