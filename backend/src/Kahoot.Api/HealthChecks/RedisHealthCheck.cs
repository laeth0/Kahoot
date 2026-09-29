namespace Kahoot.Api.HealthChecks;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry;
using StackExchange.Redis;

// Redis Health Check - Validates Redis connection multiplexer and ping response for presence, locks, and pub/sub health.
public sealed class RedisHealthCheck : IHealthCheck
{
    private readonly IConnectionMultiplexer _redis;

    public RedisHealthCheck(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    // Redis Ping Probe - Issues asynchronous PING command to verify Redis round-trip latency and multiplexer connectivity.
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Trace Suppression - Silences health ping telemetry spans to prevent OpenTelemetry buffer bloat.
            using (SuppressInstrumentationScope.Begin())
            {
                await _redis.GetDatabase().PingAsync().WaitAsync(cancellationToken);
            }
            return HealthCheckResult.Healthy();
        }
        // Non-Cancellation Guard - Traps network and timeout exceptions without masking application host cancellation requests.
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("Redis is unavailable.", exception);
        }
    }
}
