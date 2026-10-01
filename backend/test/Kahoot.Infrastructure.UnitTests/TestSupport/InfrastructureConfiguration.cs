namespace Kahoot.Infrastructure.UnitTests.TestSupport;

using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;

public static class InfrastructureConfiguration
{
    public static Dictionary<string, string?> CreateValidConfigurationDictionary()
    {
        byte[] keyBytes = RandomNumberGenerator.GetBytes(32);
        string base64Key = Convert.ToBase64String(keyBytes);

        return new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Host=127.0.0.1;Database=unit_test_model;Username=unit_test",
            ["Database:CommandTimeoutSeconds"] = "30",
            ["Database:MigrationCommandTimeoutSeconds"] = "300",
            ["BootstrapAdmin:Enabled"] = "false",
            ["Jwt:Issuer"] = "unit-test-issuer",
            ["Jwt:Audience"] = "unit-test-audience",
            ["Jwt:SigningKey"] = base64Key,
            ["Jwt:AccessTokenMinutes"] = "15",
            ["RefreshToken:LifetimeDays"] = "14",
            ["RefreshToken:FamilyMaxLifetimeDays"] = "30",
            ["GameJoin:ClientBaseUrl"] = "https://quiz.example.test",
            ["Realtime:RedisConnectionString"] = "127.0.0.1:6379",
            ["Realtime:ChannelPrefix"] = "kahoot_unit_tests"
        };
    }

    public static IConfiguration BuildConfiguration(Dictionary<string, string?> dictionary)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(dictionary)
            .Build();
    }
}
