namespace Kahoot.Api.UnitTests.ServiceCollectionExtension;

using System;
using System.Collections.Generic;
using Kahoot.Api.ServiceCollectionExtension;
using Kahoot.Api.UnitTests.TestSupport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

public sealed class ObservabilityInstallerTests
{
    [Fact]
    public void AddObservability_RejectsUnsupportedSampler()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OTEL_TRACES_SAMPLER"] = "unsupported_custom_sampler"
            })
            .Build();

        StubHostEnvironment environment = new StubHostEnvironment();
        ServiceCollection services = new ServiceCollection();
        ILoggingBuilder loggingBuilder = GetLoggingBuilder(services);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddObservability(configuration, environment, loggingBuilder));

        Assert.Equal("OTEL_TRACES_SAMPLER is not a supported sampler.", exception.Message);
    }

    [Theory]
    [InlineData("not-a-number")]
    [InlineData("0,1")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-0.01")]
    [InlineData("1.01")]
    public void AddObservability_RejectsInvalidSamplerRatio(string invalidRatio)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OTEL_TRACES_SAMPLER"] = "traceidratio",
                ["OTEL_TRACES_SAMPLER_ARG"] = invalidRatio
            })
            .Build();

        StubHostEnvironment environment = new StubHostEnvironment();
        ServiceCollection services = new ServiceCollection();
        ILoggingBuilder loggingBuilder = GetLoggingBuilder(services);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            services.AddObservability(configuration, environment, loggingBuilder));

        Assert.Equal("OTEL_TRACES_SAMPLER_ARG must be a number from 0 to 1.", exception.Message);
    }

    [Theory]
    [InlineData("always_on", null, "Development")]
    [InlineData("always_off", null, "Development")]
    [InlineData("traceidratio", "0.1", "Development")]
    [InlineData("traceidratio", "0", "Development")]
    [InlineData("traceidratio", "1", "Development")]
    [InlineData("parentbased_always_on", null, "Development")]
    [InlineData("parentbased_always_off", null, "Development")]
    [InlineData("parentbased_traceidratio", "0.25", "Development")]
    [InlineData("  ALWAYS_ON  ", null, "Development")]
    [InlineData("  PARENTBASED_TRACEIDRATIO  ", "0.5", "Development")]
    [InlineData(null, null, "Development")]
    [InlineData(null, null, "Production")]
    public void AddObservability_AcceptsSupportedConfigurationWithoutStartingProviders(
        string? samplerType,
        string? samplerArg,
        string environmentName)
    {
        Dictionary<string, string?> configValues = new Dictionary<string, string?>();
        if (samplerType is not null)
        {
            configValues["OTEL_TRACES_SAMPLER"] = samplerType;
        }

        if (samplerArg is not null)
        {
            configValues["OTEL_TRACES_SAMPLER_ARG"] = samplerArg;
        }

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        StubHostEnvironment environment = new StubHostEnvironment { EnvironmentName = environmentName };
        ServiceCollection services = new ServiceCollection();
        ILoggingBuilder loggingBuilder = GetLoggingBuilder(services);

        IServiceCollection result = services.AddObservability(configuration, environment, loggingBuilder);

        Assert.Same(services, result);
    }

    [Fact]
    public void AddObservability_SuppressesInformationalHostingRequestLogs()
    {
        IConfiguration configuration = new ConfigurationBuilder().Build();
        StubHostEnvironment environment = new StubHostEnvironment();
        ServiceCollection services = new ServiceCollection();
        ILoggingBuilder loggingBuilder = GetLoggingBuilder(services);

        services.AddObservability(configuration, environment, loggingBuilder);

        using ServiceProvider provider = services.BuildServiceProvider();
        IOptions<LoggerFilterOptions> filterOptions = provider.GetRequiredService<IOptions<LoggerFilterOptions>>();

        Assert.Contains(filterOptions.Value.Rules, rule =>
            string.Equals(rule.CategoryName, "Microsoft.AspNetCore.Hosting.Diagnostics", StringComparison.Ordinal)
            && rule.LogLevel == LogLevel.Warning);
    }

    private static ILoggingBuilder GetLoggingBuilder(IServiceCollection services)
    {
        ILoggingBuilder? capturedBuilder = null;
        services.AddLogging(builder => capturedBuilder = builder);
        return capturedBuilder!;
    }
}
