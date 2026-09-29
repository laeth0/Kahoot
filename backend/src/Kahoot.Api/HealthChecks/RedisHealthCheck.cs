namespace Kahoot.Api.HealthChecks;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry;
using StackExchange.Redis;

public sealed class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _redis;

    public RedisHealthCheck(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using (SuppressInstrumentationScope.Begin())
            {
                await _redis.GetDatabase().PingAsync().WaitAsync(cancellationToken);
            }
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("Redis is unavailable.", exception);
        }
    }
}
