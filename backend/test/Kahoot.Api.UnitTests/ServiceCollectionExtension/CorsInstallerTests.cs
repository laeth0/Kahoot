namespace Kahoot.Api.UnitTests.ServiceCollectionExtension;

using System;
using Kahoot.Api.Options;
using Kahoot.Api.ServiceCollectionExtension;
using Kahoot.Api.UnitTests.TestSupport;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

public sealed class CorsInstallerTests
{
    [Fact]
    public void AddCorsPolicy_BindsConfiguredAllowedOrigins()
    {
        IConfiguration configuration = ApiConfiguration.Create(allowedOrigins: new string[]
        {
            "https://frontend.example.test",
            "https://admin.example.test"
        });

        ServiceCollection services = new ServiceCollection();
        services.AddCorsPolicy(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();
        IOptions<Kahoot.Api.Options.CorsOptions> options = provider.GetRequiredService<IOptions<Kahoot.Api.Options.CorsOptions>>();

        Assert.NotNull(options.Value);
        Assert.Equal(2, options.Value.AllowedOrigins.Length);
        Assert.Contains("https://frontend.example.test", options.Value.AllowedOrigins);
        Assert.Contains("https://admin.example.test", options.Value.AllowedOrigins);
    }

    [Fact]
    public void AddCorsPolicy_RegistersFrontendNamedPolicy_WithCredentialsAndAnyHeaderMethod()
    {
        IConfiguration configuration = ApiConfiguration.Create(allowedOrigins: new string[]
        {
            "https://quiz.example.test"
        });

        ServiceCollection services = new ServiceCollection();
        services.AddCorsPolicy(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();
        IOptions<Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions> corsOptions = provider
            .GetRequiredService<IOptions<Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions>>();

        CorsPolicy? policy = corsOptions.Value.GetPolicy("Frontend");

        Assert.NotNull(policy);
        Assert.True(policy.SupportsCredentials);
        Assert.True(policy.AllowAnyHeader);
        Assert.True(policy.AllowAnyMethod);
        Assert.False(policy.AllowAnyOrigin);
        Assert.True(policy.IsOriginAllowed("https://quiz.example.test"));
        Assert.False(policy.IsOriginAllowed("https://malicious.example.com"));
    }

    [Fact]
    public void AddCorsPolicy_WithEmptyAllowedOrigins_DoesNotProducePermissivePolicy()
    {
        IConfiguration configuration = ApiConfiguration.Create(allowedOrigins: Array.Empty<string>());

        ServiceCollection services = new ServiceCollection();
        services.AddCorsPolicy(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();
        IOptions<Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions> corsOptions = provider
            .GetRequiredService<IOptions<Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions>>();

        CorsPolicy? policy = corsOptions.Value.GetPolicy("Frontend");

        Assert.NotNull(policy);
        Assert.True(policy.SupportsCredentials);
        Assert.False(policy.AllowAnyOrigin);
        Assert.Empty(policy.Origins);
        Assert.False(policy.IsOriginAllowed("https://quiz.example.test"));
        Assert.False(policy.IsOriginAllowed("https://any.example.com"));
    }

    [Fact]
    public void AddCorsPolicy_WildcardOriginWithCredentials_ThrowsInvalidOperationExceptionOnPolicyConstruction()
    {
        IConfiguration configuration = ApiConfiguration.Create(allowedOrigins: new string[] { "*" });

        ServiceCollection services = new ServiceCollection();
        services.AddCorsPolicy(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();
        IOptions<Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions> corsOptions = provider
            .GetRequiredService<IOptions<Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions>>();

        Assert.Throws<InvalidOperationException>(() => corsOptions.Value.GetPolicy("Frontend"));
    }
}
