using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Kahoot.Api.Common;

internal static class RateLimitingExtensions
{
    public const string AuthPolicy = "auth";
    public const string JoinPolicy = "join";

    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetTokenBucketLimiter(
                    ClientKey(context),
                    _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = 2000,
                        TokensPerPeriod = 1000,
                        ReplenishmentPeriod = TimeSpan.FromSeconds(10),
                        QueueLimit = 200,
                        AutoReplenishment = true
                    }));

            options.AddPolicy(AuthPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    ClientKey(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 100,
                        Window = TimeSpan.FromMinutes(5),
                        QueueLimit = 20
                    }));

            options.AddPolicy(JoinPolicy, context =>
                RateLimitPartition.GetTokenBucketLimiter(
                    ClientKey(context),
                    _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = 1000,
                        TokensPerPeriod = 500,
                        ReplenishmentPeriod = TimeSpan.FromSeconds(10),
                        QueueLimit = 200,
                        AutoReplenishment = true
                    }));
        });

        return services;
    }

    private static string ClientKey(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
