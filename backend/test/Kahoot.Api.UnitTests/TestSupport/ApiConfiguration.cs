namespace Kahoot.Api.UnitTests.TestSupport;

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

public static class ApiConfiguration
{
    public static IConfiguration Create(
        IEnumerable<string>? allowedOrigins = null,
        string issuer = "https://auth.kahoot-saas.local",
        string audience = "https://api.kahoot-saas.local",
        string? signingKey = null,
        IDictionary<string, string?>? additionalValues = null)
    {
        string validKey = signingKey ?? Convert.ToBase64String(new byte[32]
        {
            1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16,
            17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32
        });

        Dictionary<string, string?> values = new()
        {
            ["Jwt:Issuer"] = issuer,
            ["Jwt:Audience"] = audience,
            ["Jwt:SigningKey"] = validKey,
            ["Jwt:AccessTokenMinutes"] = "15"
        };

        if (allowedOrigins is not null)
        {
            int index = 0;
            foreach (string origin in allowedOrigins)
            {
                values[$"Cors:AllowedOrigins:{index}"] = origin;
                index++;
            }
        }
        else
        {
            values["Cors:AllowedOrigins:0"] = "https://quiz.example.test";
        }

        if (additionalValues is not null)
        {
            foreach (KeyValuePair<string, string?> kvp in additionalValues)
            {
                values[kvp.Key] = kvp.Value;
            }
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
