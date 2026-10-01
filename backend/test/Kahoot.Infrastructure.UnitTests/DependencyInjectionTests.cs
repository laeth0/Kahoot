namespace Kahoot.Infrastructure.UnitTests;

using System;
using System.Collections.Generic;
using System.Linq;
using Kahoot.Application.Common.Interfaces;
using Kahoot.Infrastructure;
using Kahoot.Infrastructure.Persistence;
using Kahoot.Infrastructure.Realtime;
using Kahoot.Infrastructure.Services;
using Kahoot.Infrastructure.UnitTests.TestSupport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddInfrastructure_WithValidConfiguration_RegistersRootServicesSuccessfully()
    {
        ServiceCollection services = new();
        IConfiguration configuration = InfrastructureConfiguration.BuildConfiguration(
            InfrastructureConfiguration.CreateValidConfigurationDictionary());

        services.AddInfrastructure(configuration);

        ServiceDescriptor? trackerDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ICriticalWorkerFailureTracker));
        Assert.NotNull(trackerDescriptor);
        Assert.Equal(typeof(CriticalWorkerFailureTracker), trackerDescriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, trackerDescriptor.Lifetime);

        ServiceDescriptor? channelDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ISuspensionFinalizerChannel));
        Assert.NotNull(channelDescriptor);
        Assert.Equal(typeof(SuspensionFinalizerChannel), channelDescriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, channelDescriptor.Lifetime);

        ServiceDescriptor? evictionDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ISocketEvictionService));
        Assert.NotNull(evictionDescriptor);
        Assert.Equal(typeof(SocketEvictionService), evictionDescriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, evictionDescriptor.Lifetime);

        ServiceDescriptor? timeProviderDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(TimeProvider));
        Assert.NotNull(timeProviderDescriptor);
        Assert.Equal(ServiceLifetime.Singleton, timeProviderDescriptor.Lifetime);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("false")]
    public void AddInfrastructure_DisasterRecoveryAbsentOrFalse_ProceedsWithRegistration(string? drValue)
    {
        Dictionary<string, string?> config = InfrastructureConfiguration.CreateValidConfigurationDictionary();
        if (drValue is not null)
        {
            config["DR_RECONCILIATION_ON_STARTUP"] = drValue;
        }

        ServiceCollection services = new();
        services.AddInfrastructure(InfrastructureConfiguration.BuildConfiguration(config));

        Assert.NotEmpty(services);
    }

    [Fact]
    public void AddInfrastructure_MalformedDisasterRecoverySetting_ThrowsAndAbortsBeforeRegistration()
    {
        Dictionary<string, string?> config = InfrastructureConfiguration.CreateValidConfigurationDictionary();
        config["DR_RECONCILIATION_ON_STARTUP"] = "not-a-boolean-value";

        ServiceCollection services = new();
        int initialCount = services.Count;

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
        {
            services.AddInfrastructure(InfrastructureConfiguration.BuildConfiguration(config));
        });

        Assert.Equal("DR_RECONCILIATION_ON_STARTUP must be true or false.", ex.Message);
        Assert.Equal(initialCount, services.Count);
    }

    [Fact]
    public void AddInfrastructure_DisasterRecoveryEnabled_ThrowsSecurityLedgerReconciliationAndAbortsBeforeRegistration()
    {
        Dictionary<string, string?> config = InfrastructureConfiguration.CreateValidConfigurationDictionary();
        config["DR_RECONCILIATION_ON_STARTUP"] = "true";

        ServiceCollection services = new();
        int initialCount = services.Count;

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
        {
            services.AddInfrastructure(InfrastructureConfiguration.BuildConfiguration(config));
        });

        Assert.Contains("Disaster recovery admission requires reconciliation against an independently backed-up security ledger", ex.Message);
        Assert.Equal(initialCount, services.Count);
    }
}
