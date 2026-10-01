namespace Kahoot.Infrastructure.UnitTests.ServiceCollectionExtension;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Options;
using Kahoot.Infrastructure.Security;
using Kahoot.Infrastructure.ServiceCollectionExtension;
using Kahoot.Infrastructure.UnitTests.TestSupport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

public sealed class SecurityInstallerTests
{
    [Fact]
    public void AddSecurity_RegistersExpectedSingletonDescriptors()
    {
        ServiceCollection services = new();
        IConfiguration configuration = InfrastructureConfiguration.BuildConfiguration(
            InfrastructureConfiguration.CreateValidConfigurationDictionary());

        services.AddSecurity(configuration);

        ServiceDescriptor? passwordHasherDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IPasswordHasher));
        Assert.NotNull(passwordHasherDescriptor);
        Assert.Equal(typeof(PasswordHasher), passwordHasherDescriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, passwordHasherDescriptor.Lifetime);

        ServiceDescriptor? jwtGeneratorDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IJwtTokenGenerator));
        Assert.NotNull(jwtGeneratorDescriptor);
        Assert.Equal(typeof(JwtTokenGenerator), jwtGeneratorDescriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, jwtGeneratorDescriptor.Lifetime);
    }

    [Fact]
    public void AddSecurity_ValidBaselineConfiguration_ResolvesOptionsSuccessfully()
    {
        ServiceCollection services = new();
        IConfiguration configuration = InfrastructureConfiguration.BuildConfiguration(
            InfrastructureConfiguration.CreateValidConfigurationDictionary());

        services.AddSecurity(configuration);
        using ServiceProvider provider = services.BuildServiceProvider();

        JwtOptions jwtOptions = provider.GetRequiredService<IOptions<JwtOptions>>().Value;
        Assert.Equal("unit-test-issuer", jwtOptions.Issuer);
        Assert.Equal("unit-test-audience", jwtOptions.Audience);
        Assert.Equal(15, jwtOptions.AccessTokenMinutes);

        RefreshTokenOptions refreshOptions = provider.GetRequiredService<IOptions<RefreshTokenOptions>>().Value;
        Assert.Equal(14, refreshOptions.LifetimeDays);
        Assert.Equal(30, refreshOptions.FamilyMaxLifetimeDays);

        BootstrapAdminOptions bootstrapOptions = provider.GetRequiredService<IOptions<BootstrapAdminOptions>>().Value;
        Assert.False(bootstrapOptions.Enabled);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AddSecurity_RejectsEmptyOrWhitespaceJwtIssuer(string invalidIssuer)
    {
        Dictionary<string, string?> config = InfrastructureConfiguration.CreateValidConfigurationDictionary();
        config["Jwt:Issuer"] = invalidIssuer;

        AssertThrowsOptionsValidation<JwtOptions>(config);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AddSecurity_RejectsEmptyOrWhitespaceJwtAudience(string invalidAudience)
    {
        Dictionary<string, string?> config = InfrastructureConfiguration.CreateValidConfigurationDictionary();
        config["Jwt:Audience"] = invalidAudience;

        AssertThrowsOptionsValidation<JwtOptions>(config);
    }

    [Fact]
    public void AddSecurity_RejectsMalformedBase64SigningKey()
    {
        Dictionary<string, string?> config = InfrastructureConfiguration.CreateValidConfigurationDictionary();
        config["Jwt:SigningKey"] = "!@#$%^&*()_+";

        AssertThrowsOptionsValidation<JwtOptions>(config);
    }

    [Fact]
    public void AddSecurity_RejectsThirtyOneByteSigningKey()
    {
        byte[] shortKey = RandomNumberGenerator.GetBytes(31);
        Dictionary<string, string?> config = InfrastructureConfiguration.CreateValidConfigurationDictionary();
        config["Jwt:SigningKey"] = Convert.ToBase64String(shortKey);

        AssertThrowsOptionsValidation<JwtOptions>(config);
    }

    [Theory]
    [InlineData(32)]
    [InlineData(64)]
    public void AddSecurity_AcceptsThirtyTwoAndSixtyFourByteSigningKey(int keyLength)
    {
        byte[] validKey = RandomNumberGenerator.GetBytes(keyLength);
        Dictionary<string, string?> config = InfrastructureConfiguration.CreateValidConfigurationDictionary();
        config["Jwt:SigningKey"] = Convert.ToBase64String(validKey);

        ServiceCollection services = new();
        services.AddSecurity(InfrastructureConfiguration.BuildConfiguration(config));
        using ServiceProvider provider = services.BuildServiceProvider();

        JwtOptions options = provider.GetRequiredService<IOptions<JwtOptions>>().Value;
        Assert.Equal(Convert.ToBase64String(validKey), options.SigningKey);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddSecurity_RejectsNonPositiveAccessTokenMinutes(int invalidMinutes)
    {
        Dictionary<string, string?> config = InfrastructureConfiguration.CreateValidConfigurationDictionary();
        config["Jwt:AccessTokenMinutes"] = invalidMinutes.ToString();

        AssertThrowsOptionsValidation<JwtOptions>(config);
    }

    [Fact]
    public void AddSecurity_AcceptsCustomPositiveAccessTokenMinutes()
    {
        Dictionary<string, string?> config = InfrastructureConfiguration.CreateValidConfigurationDictionary();
        config["Jwt:AccessTokenMinutes"] = "45";

        ServiceCollection services = new();
        services.AddSecurity(InfrastructureConfiguration.BuildConfiguration(config));
        using ServiceProvider provider = services.BuildServiceProvider();

        JwtOptions options = provider.GetRequiredService<IOptions<JwtOptions>>().Value;
        Assert.Equal(45, options.AccessTokenMinutes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddSecurity_RejectsNonPositiveRefreshTokenLifetimeDays(int invalidLifetime)
    {
        Dictionary<string, string?> config = InfrastructureConfiguration.CreateValidConfigurationDictionary();
        config["RefreshToken:LifetimeDays"] = invalidLifetime.ToString();

        AssertThrowsOptionsValidation<RefreshTokenOptions>(config);
    }

    [Fact]
    public void AddSecurity_RejectsFamilyMaxLifetimeDaysLessThanLifetimeDays()
    {
        Dictionary<string, string?> config = InfrastructureConfiguration.CreateValidConfigurationDictionary();
        config["RefreshToken:LifetimeDays"] = "14";
        config["RefreshToken:FamilyMaxLifetimeDays"] = "10";

        AssertThrowsOptionsValidation<RefreshTokenOptions>(config);
    }

    [Fact]
    public void AddSecurity_AcceptsFamilyMaxLifetimeDaysEqualToLifetimeDays()
    {
        Dictionary<string, string?> config = InfrastructureConfiguration.CreateValidConfigurationDictionary();
        config["RefreshToken:LifetimeDays"] = "14";
        config["RefreshToken:FamilyMaxLifetimeDays"] = "14";

        ServiceCollection services = new();
        services.AddSecurity(InfrastructureConfiguration.BuildConfiguration(config));
        using ServiceProvider provider = services.BuildServiceProvider();

        RefreshTokenOptions options = provider.GetRequiredService<IOptions<RefreshTokenOptions>>().Value;
        Assert.Equal(14, options.LifetimeDays);
        Assert.Equal(14, options.FamilyMaxLifetimeDays);
    }

    [Fact]
    public void AddSecurity_EnabledBootstrapWithBlankUsername_ThrowsOptionsValidation()
    {
        Dictionary<string, string?> config = InfrastructureConfiguration.CreateValidConfigurationDictionary();
        config["BootstrapAdmin:Enabled"] = "true";
        config["BootstrapAdmin:Username"] = "";
        config["BootstrapAdmin:Password"] = new string('A', 16);

        AssertThrowsOptionsValidation<BootstrapAdminOptions>(config);
    }

    [Theory]
    [InlineData(15)]
    [InlineData(129)]
    public void AddSecurity_EnabledBootstrapWithInvalidPasswordLength_ThrowsOptionsValidation(int length)
    {
        Dictionary<string, string?> config = InfrastructureConfiguration.CreateValidConfigurationDictionary();
        config["BootstrapAdmin:Enabled"] = "true";
        config["BootstrapAdmin:Username"] = "AdminUser";
        config["BootstrapAdmin:Password"] = new string('A', length);

        AssertThrowsOptionsValidation<BootstrapAdminOptions>(config);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(128)]
    public void AddSecurity_EnabledBootstrapWithValidBoundaryPasswordLength_Accepted(int length)
    {
        Dictionary<string, string?> config = InfrastructureConfiguration.CreateValidConfigurationDictionary();
        config["BootstrapAdmin:Enabled"] = "true";
        config["BootstrapAdmin:Username"] = "AdminUser";
        config["BootstrapAdmin:Password"] = new string('A', length);

        ServiceCollection services = new();
        services.AddSecurity(InfrastructureConfiguration.BuildConfiguration(config));
        using ServiceProvider provider = services.BuildServiceProvider();

        BootstrapAdminOptions options = provider.GetRequiredService<IOptions<BootstrapAdminOptions>>().Value;
        Assert.True(options.Enabled);
        Assert.Equal("AdminUser", options.Username);
        Assert.Equal(length, options.Password.Length);
    }

    [Fact]
    public void AddSecurity_DisabledBootstrapDoesNotRequireCredentials()
    {
        Dictionary<string, string?> config = InfrastructureConfiguration.CreateValidConfigurationDictionary();
        config["BootstrapAdmin:Enabled"] = "false";
        config["BootstrapAdmin:Username"] = "";
        config["BootstrapAdmin:Password"] = "";

        ServiceCollection services = new();
        services.AddSecurity(InfrastructureConfiguration.BuildConfiguration(config));
        using ServiceProvider provider = services.BuildServiceProvider();

        BootstrapAdminOptions options = provider.GetRequiredService<IOptions<BootstrapAdminOptions>>().Value;
        Assert.False(options.Enabled);
    }

    [Fact]
    public void AddSecurity_EnvironmentVariablesOverrideNestedBootstrapOptions()
    {
        Dictionary<string, string?> config = InfrastructureConfiguration.CreateValidConfigurationDictionary();
        config["BootstrapAdmin:Enabled"] = "false";
        config["BOOTSTRAP_ADMIN_ENABLED"] = "true";
        config["BOOTSTRAP_ADMIN_USERNAME"] = "EnvAdmin";
        config["BOOTSTRAP_ADMIN_PASSWORD"] = new string('B', 20);

        ServiceCollection services = new();
        services.AddSecurity(InfrastructureConfiguration.BuildConfiguration(config));
        using ServiceProvider provider = services.BuildServiceProvider();

        BootstrapAdminOptions options = provider.GetRequiredService<IOptions<BootstrapAdminOptions>>().Value;
        Assert.True(options.Enabled);
        Assert.Equal("EnvAdmin", options.Username);
        Assert.Equal(new string('B', 20), options.Password);
    }

    [Fact]
    public void AddSecurity_MalformedBootstrapAdminEnabledEnvVar_ThrowsInvalidOperationException()
    {
        Dictionary<string, string?> config = InfrastructureConfiguration.CreateValidConfigurationDictionary();
        config["BOOTSTRAP_ADMIN_ENABLED"] = "not-a-bool";

        ServiceCollection services = new();
        services.AddSecurity(InfrastructureConfiguration.BuildConfiguration(config));
        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(() =>
        {
            _ = provider.GetRequiredService<IOptions<BootstrapAdminOptions>>().Value;
        });
    }

    [Theory]
    [InlineData("Jwt")]
    [InlineData("RefreshToken")]
    [InlineData("BootstrapAdmin")]
    public void AddSecurity_MissingRequiredSection_ThrowsInvalidOperationException(string sectionToRemove)
    {
        Dictionary<string, string?> config = InfrastructureConfiguration.CreateValidConfigurationDictionary();
        List<string> keysToRemove = config.Keys.Where(k => k.StartsWith(sectionToRemove + ":", StringComparison.OrdinalIgnoreCase)).ToList();
        foreach (string key in keysToRemove)
        {
            config.Remove(key);
        }

        ServiceCollection services = new();
        Assert.Throws<InvalidOperationException>(() =>
        {
            services.AddSecurity(InfrastructureConfiguration.BuildConfiguration(config));
        });
    }

    private static void AssertThrowsOptionsValidation<TOptions>(Dictionary<string, string?> config) where TOptions : class
    {
        ServiceCollection services = new();
        services.AddSecurity(InfrastructureConfiguration.BuildConfiguration(config));
        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() =>
        {
            _ = provider.GetRequiredService<IOptions<TOptions>>().Value;
        });
    }
}
