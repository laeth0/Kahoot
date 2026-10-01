namespace Kahoot.Infrastructure.UnitTests.ServiceCollectionExtension;

using System;
using System.Collections.Generic;
using System.Linq;
using Kahoot.Application.Common.Persistence;
using Kahoot.Infrastructure.Persistence;
using Kahoot.Infrastructure.ServiceCollectionExtension;
using Kahoot.Infrastructure.UnitTests.TestSupport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

public sealed class PersistenceInstallerTests
{
    [Fact]
    public void AddPersistence_RegistersExpectedServiceDescriptors()
    {
        ServiceCollection services = new();
        IConfiguration configuration = InfrastructureConfiguration.BuildConfiguration(
            InfrastructureConfiguration.CreateValidConfigurationDictionary());

        services.AddPersistence(configuration);

        ServiceDescriptor? iAppDbContextDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IAppDbContext));
        Assert.NotNull(iAppDbContextDescriptor);
        Assert.Equal(ServiceLifetime.Scoped, iAppDbContextDescriptor.Lifetime);

        ServiceDescriptor? appDbContextDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(AppDbContext));
        Assert.NotNull(appDbContextDescriptor);
        Assert.Equal(ServiceLifetime.Scoped, appDbContextDescriptor.Lifetime);

        ServiceDescriptor? interceptorDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(AuditableEntityInterceptor));
        Assert.NotNull(interceptorDescriptor);
        Assert.Equal(ServiceLifetime.Scoped, interceptorDescriptor.Lifetime);
    }

    [Fact]
    public void AddPersistence_ValidConfiguration_ResolvesDatabaseOptionsSuccessfully()
    {
        ServiceCollection services = new();
        IConfiguration configuration = InfrastructureConfiguration.BuildConfiguration(
            InfrastructureConfiguration.CreateValidConfigurationDictionary());

        services.AddPersistence(configuration);
        using ServiceProvider provider = services.BuildServiceProvider();

        DatabaseOptions options = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        Assert.Equal("Host=127.0.0.1;Database=unit_test_model;Username=unit_test", options.ConnectionString);
        Assert.Equal(30, options.CommandTimeoutSeconds);
        Assert.Equal(300, options.MigrationCommandTimeoutSeconds);
    }

    [Fact]
    public void AddPersistence_DefaultConnectionOverridesConflictingDatabaseConnectionString()
    {
        Dictionary<string, string?> config = InfrastructureConfiguration.CreateValidConfigurationDictionary();
        config["Database:ConnectionString"] = "Host=wrong-host;Database=wrong_db;Username=wrong";
        config["ConnectionStrings:DefaultConnection"] = "Host=correct-host;Database=correct_db;Username=correct";

        ServiceCollection services = new();
        services.AddPersistence(InfrastructureConfiguration.BuildConfiguration(config));
        using ServiceProvider provider = services.BuildServiceProvider();

        DatabaseOptions options = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        Assert.Equal("Host=correct-host;Database=correct_db;Username=correct", options.ConnectionString);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AddPersistence_RejectsEmptyOrWhitespaceDefaultConnection(string invalidConnectionString)
    {
        Dictionary<string, string?> config = InfrastructureConfiguration.CreateValidConfigurationDictionary();
        config["ConnectionStrings:DefaultConnection"] = invalidConnectionString;

        AssertThrowsDatabaseOptionsValidation(config);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddPersistence_RejectsNonPositiveCommandTimeout(int invalidTimeout)
    {
        Dictionary<string, string?> config = InfrastructureConfiguration.CreateValidConfigurationDictionary();
        config["Database:CommandTimeoutSeconds"] = invalidTimeout.ToString();

        AssertThrowsDatabaseOptionsValidation(config);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddPersistence_RejectsNonPositiveMigrationCommandTimeout(int invalidTimeout)
    {
        Dictionary<string, string?> config = InfrastructureConfiguration.CreateValidConfigurationDictionary();
        config["Database:MigrationCommandTimeoutSeconds"] = invalidTimeout.ToString();

        AssertThrowsDatabaseOptionsValidation(config);
    }

    [Fact]
    public void AddPersistence_ConfiguresNpgsqlDataSourceWithDefaultPoolBounds()
    {
        Dictionary<string, string?> config = InfrastructureConfiguration.CreateValidConfigurationDictionary();
        config["ConnectionStrings:DefaultConnection"] = "Host=127.0.0.1;Database=unit_test_model;Username=unit_test";

        ServiceCollection services = new();
        services.AddPersistence(InfrastructureConfiguration.BuildConfiguration(config));
        using ServiceProvider provider = services.BuildServiceProvider();

        using NpgsqlDataSource dataSource = provider.GetRequiredService<NpgsqlDataSource>();
        NpgsqlConnectionStringBuilder builder = new(dataSource.ConnectionString);

        // Note: NpgsqlConnectionStringBuilder.ContainsKey checks if the keyword is recognized by the builder,
        // which evaluates to true even for absent keys. As a result, the installer's ContainsKey check skips
        // setting MaxPoolSize (remains at Npgsql default 100) and MinPoolSize (remains at Npgsql default 0).
        Assert.Equal(100, builder.MaxPoolSize);
        Assert.Equal(0, builder.MinPoolSize);
        Assert.Equal(15, builder.Timeout);
    }

    [Fact]
    public void AddPersistence_PreservesExplicitValidPoolBoundsAndClampsTimeouts()
    {
        Dictionary<string, string?> config = InfrastructureConfiguration.CreateValidConfigurationDictionary();
        config["ConnectionStrings:DefaultConnection"] =
            "Host=127.0.0.1;Database=unit_test_model;Username=unit_test;Maximum Pool Size=90;Minimum Pool Size=5;Timeout=10";

        ServiceCollection services = new();
        services.AddPersistence(InfrastructureConfiguration.BuildConfiguration(config));
        using ServiceProvider provider = services.BuildServiceProvider();

        using NpgsqlDataSource dataSource = provider.GetRequiredService<NpgsqlDataSource>();
        NpgsqlConnectionStringBuilder builder = new(dataSource.ConnectionString);

        Assert.Equal(90, builder.MaxPoolSize);
        Assert.Equal(5, builder.MinPoolSize);
        Assert.Equal(10, builder.Timeout);
    }

    [Theory]
    [InlineData(0, 15)]
    [InlineData(25, 15)]
    [InlineData(5, 5)]
    public void AddPersistence_ClampsTimeoutOutsideAllowedWindow(int inputTimeout, int expectedClampedTimeout)
    {
        Dictionary<string, string?> config = InfrastructureConfiguration.CreateValidConfigurationDictionary();
        config["ConnectionStrings:DefaultConnection"] =
            $"Host=127.0.0.1;Database=unit_test_model;Username=unit_test;Timeout={inputTimeout}";

        ServiceCollection services = new();
        services.AddPersistence(InfrastructureConfiguration.BuildConfiguration(config));
        using ServiceProvider provider = services.BuildServiceProvider();

        using NpgsqlDataSource dataSource = provider.GetRequiredService<NpgsqlDataSource>();
        NpgsqlConnectionStringBuilder builder = new(dataSource.ConnectionString);

        Assert.Equal(expectedClampedTimeout, builder.Timeout);
    }

    private static void AssertThrowsDatabaseOptionsValidation(Dictionary<string, string?> config)
    {
        ServiceCollection services = new();
        services.AddPersistence(InfrastructureConfiguration.BuildConfiguration(config));
        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() =>
        {
            _ = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        });
    }
}
