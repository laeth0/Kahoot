namespace Kahoot.Api.UnitTests.ServiceCollectionExtension;

using System;
using System.Collections.Generic;
using Kahoot.Api.ServiceCollectionExtension;
using Kahoot.Api.UnitTests.TestSupport;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Xunit;

public sealed class JwtAuthenticationInstallerTests
{
    [Fact]
    public void AddJwtAuthentication_ConfiguresExpectedTokenValidationParameters()
    {
        byte[] expectedKeyBytes = new byte[32]
        {
            10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110, 120, 130, 140, 150, 160,
            170, 180, 190, 200, 210, 220, 230, 240, 250, 1, 2, 3, 4, 5, 6, 7
        };
        string signingKeyBase64 = Convert.ToBase64String(expectedKeyBytes);

        IConfiguration configuration = ApiConfiguration.Create(
            issuer: "https://custom-auth.example.test",
            audience: "https://custom-api.example.test",
            signingKey: signingKeyBase64);

        ServiceCollection services = new ServiceCollection();
        services.AddJwtAuthentication(configuration);

        using ServiceProvider provider = services.BuildServiceProvider();
        IOptionsMonitor<JwtBearerOptions> monitor = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>();
        JwtBearerOptions options = monitor.Get(JwtBearerDefaults.AuthenticationScheme);

        Assert.False(options.MapInboundClaims);
        Assert.NotNull(options.TokenValidationParameters);
        Assert.True(options.TokenValidationParameters.ValidateIssuer);
        Assert.Equal("https://custom-auth.example.test", options.TokenValidationParameters.ValidIssuer);
        Assert.True(options.TokenValidationParameters.ValidateAudience);
        Assert.Equal("https://custom-api.example.test", options.TokenValidationParameters.ValidAudience);
        Assert.True(options.TokenValidationParameters.ValidateIssuerSigningKey);
        SymmetricSecurityKey symmetricKey = Assert.IsType<SymmetricSecurityKey>(options.TokenValidationParameters.IssuerSigningKey);
        Assert.Equal(expectedKeyBytes, symmetricKey.Key);
        Assert.True(options.TokenValidationParameters.ValidateLifetime);
        Assert.Equal("role", options.TokenValidationParameters.RoleClaimType);
        Assert.Equal(TimeSpan.Zero, options.TokenValidationParameters.ClockSkew);
    }

    [Fact]
    public void AddJwtAuthentication_WithInvalidBase64SigningKey_ThrowsFormatException()
    {
        IConfiguration configuration = ApiConfiguration.Create(signingKey: "Not_A_Valid_Base64_String!@#$");

        ServiceCollection services = new ServiceCollection();

        Assert.Throws<FormatException>(() => services.AddJwtAuthentication(configuration));
    }
}
