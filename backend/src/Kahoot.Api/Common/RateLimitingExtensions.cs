using System.Net;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
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

            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.ContentType = "application/problem+json";

                int retryAfterSeconds = 30;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter) && retryAfter > TimeSpan.Zero)
                {
                    retryAfterSeconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
                }

                context.HttpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString();
                context.HttpContext.Response.ContentType = "application/problem+json";

                var problem = new ProblemDetails
                {
                    Type = "https://tools.ietf.org/html/rfc6585#section-4",
                    Title = "Too Many Requests",
                    Status = StatusCodes.Status429TooManyRequests,
                    Detail = $"Quota exceeded. Please retry after {retryAfterSeconds} seconds.",
                    Instance = context.HttpContext.Request.Path
                };
                problem.Extensions["correlationId"] = context.HttpContext.TraceIdentifier;

                await context.HttpContext.Response.WriteAsJsonAsync<ProblemDetails>(problem, cancellationToken);
            };

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
                    AuthClientKey(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(5),
                        QueueLimit = 0
                    }));

            options.AddPolicy(JoinPolicy, context =>
                RateLimitPartition.GetTokenBucketLimiter(
                    ClientKey(context),
                    _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = 1200,
                        TokensPerPeriod = 600,
                        ReplenishmentPeriod = TimeSpan.FromSeconds(10),
                        QueueLimit = 200,
                        AutoReplenishment = true
                    }));
        });

        return services;
    }

    private static string ClientKey(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static string AuthClientKey(HttpContext context)
    {
        string ip = ClientKey(context);
        if (context.Items.TryGetValue("AuthUsername", out object? userObj) &&
            userObj is string username &&
            !string.IsNullOrWhiteSpace(username))
        {
            return $"{ip}_{username.Trim().ToLowerInvariant()}";
        }
        return ip;
    }
}
