namespace Kahoot.Infrastructure.UnitTests.ServiceCollectionExtension;

using System;
using System.Collections.Generic;
using System.Linq;
using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Options;
using Kahoot.Infrastructure.Realtime;
using Kahoot.Infrastructure.ServiceCollectionExtension;
using Kahoot.Infrastructure.UnitTests.TestSupport;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Xunit;

public sealed class RealtimeInstallerTests
{
    [Fact]
    public void AddRealtime_RegistersExpectedLifetimes()
    {
        ServiceCollection services = new();
        IConfiguration configuration = InfrastructureConfiguration.BuildConfiguration(
            InfrastructureConfiguration.CreateValidConfigurationDictionary());

        services.AddRealtime(configuration);

        ServiceDescriptor? notificationDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IGameNotificationService));
        Assert.NotNull(notificationDescriptor);
        Assert.Equal(ServiceLifetime.Scoped, notificationDescriptor.Lifetime);

        ServiceDescriptor? pinDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IPinGeneratorService));
        Assert.NotNull(pinDescriptor);
        Assert.Equal(ServiceLifetime.Scoped, pinDescriptor.Lifetime);

        ServiceDescriptor? multiplexerDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IConnectionMultiplexer));
        Assert.NotNull(multiplexerDescriptor);
        Assert.Equal(ServiceLifetime.Singleton, multiplexerDescriptor.Lifetime);
        Assert.NotNull(multiplexerDescriptor.ImplementationFactory);

        ServiceDescriptor? socketGuardDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(UnauthenticatedSocketGuard));
        Assert.NotNull(socketGuardDescriptor);
        Assert.Equal(ServiceLifetime.Singleton, socketGuardDescriptor.Lifetime);

        ServiceDescriptor? hubFilterDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(GameHubFilter));
        Assert.NotNull(hubFilterDescriptor);
        Assert.Equal(ServiceLifetime.Singleton, hubFilterDescriptor.Lifetime);
    }

    [Fact]
    public void AddRealtime_ConfiguresSignalRHubOptionsWithoutInvokingRedisFactory()
    {
        ServiceCollection services = new();
        IConfiguration configuration = InfrastructureConfiguration.BuildConfiguration(
            InfrastructureConfiguration.CreateValidConfigurationDictionary());

        services.AddRealtime(configuration);
        using ServiceProvider provider = services.BuildServiceProvider();

        HubOptions hubOptions = provider.GetRequiredService<IOptions<HubOptions>>().Value;
        Assert.Equal(32768, hubOptions.MaximumReceiveMessageSize);
        Assert.Equal(TimeSpan.FromSeconds(15), hubOptions.KeepAliveInterval);
        Assert.Equal(TimeSpan.FromSeconds(30), hubOptions.ClientTimeoutInterval);
        Assert.Equal(TimeSpan.FromSeconds(15), hubOptions.HandshakeTimeout);
        Assert.Equal(1, hubOptions.MaximumParallelInvocationsPerClient);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AddRealtime_RejectsBlankRedisConnectionString(string blankConnectionString)
    {
        Dictionary<string, string?> config = InfrastructureConfiguration.CreateValidConfigurationDictionary();
        config["Realtime:RedisConnectionString"] = blankConnectionString;

        ServiceCollection services = new();
        services.AddRealtime(InfrastructureConfiguration.BuildConfiguration(config));
        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() =>
        {
            _ = provider.GetRequiredService<IOptions<RealtimeOptions>>().Value;
        });
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid channel prefix with spaces")]
    public void AddRealtime_RejectsInvalidChannelPrefix(string invalidPrefix)
    {
        Dictionary<string, string?> config = InfrastructureConfiguration.CreateValidConfigurationDictionary();
        config["Realtime:ChannelPrefix"] = invalidPrefix;

        ServiceCollection services = new();
        services.AddRealtime(InfrastructureConfiguration.BuildConfiguration(config));
        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() =>
        {
            _ = provider.GetRequiredService<IOptions<RealtimeOptions>>().Value;
        });
    }

    [Theory]
    [InlineData("http://remote.host.com")] // Insecure remote HTTP origin
    [InlineData("ftp://invalid.scheme")]
    [InlineData("not-a-valid-uri")]
    public void AddRealtime_RejectsInvalidGameJoinClientBaseUrl(string invalidUrl)
    {
        Dictionary<string, string?> config = InfrastructureConfiguration.CreateValidConfigurationDictionary();
        config["GameJoin:ClientBaseUrl"] = invalidUrl;

        ServiceCollection services = new();
        services.AddRealtime(InfrastructureConfiguration.BuildConfiguration(config));
        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() =>
        {
            _ = provider.GetRequiredService<IOptions<GameJoinOptions>>().Value;
        });
    }
}
