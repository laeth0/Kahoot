namespace Kahoot.Api.HealthChecks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

internal sealed class ReadinessHealthCheck : IHealthCheck
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromMilliseconds(450);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RedisHealthCheck _redis;
    private readonly StorageHealthCheck _storage;
    private readonly TimeProvider _timeProvider;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private CachedResult? _cachedResult;

    public ReadinessHealthCheck(
        IServiceScopeFactory scopeFactory,
        RedisHealthCheck redis,
        StorageHealthCheck storage,
        TimeProvider timeProvider,
        IHostApplicationLifetime lifetime)
    {
        _scopeFactory = scopeFactory;
        _redis = redis;
        _storage = storage;
        _timeProvider = timeProvider;
        _lifetime = lifetime;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!IsAcceptingTraffic())
        {
            return HealthCheckResult.Unhealthy("Application is not accepting traffic.");
        }

        CachedResult? cached = Volatile.Read(ref _cachedResult);
        if (cached is not null && _timeProvider.GetUtcNow() < cached.ExpiresAt)
        {
            return IsAcceptingTraffic()
                ? cached.Result
                : HealthCheckResult.Unhealthy("Application is not accepting traffic.");
        }

        await _refreshGate.WaitAsync(cancellationToken);
        try
        {
            cached = _cachedResult;
            if (cached is not null && _timeProvider.GetUtcNow() < cached.ExpiresAt)
            {
                return IsAcceptingTraffic()
                    ? cached.Result
                    : HealthCheckResult.Unhealthy("Application is not accepting traffic.");
            }

            HealthCheckResult result = await ProbeDependenciesAsync(context, cancellationToken);
            Volatile.Write(ref _cachedResult,
                new CachedResult(result, _timeProvider.GetUtcNow().Add(CacheDuration)));
            return IsAcceptingTraffic()
                ? result
                : HealthCheckResult.Unhealthy("Application is not accepting traffic.");
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private bool IsAcceptingTraffic() =>
        _lifetime.ApplicationStarted.IsCancellationRequested &&
        !_lifetime.ApplicationStopping.IsCancellationRequested;

    private async Task<HealthCheckResult> ProbeDependenciesAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken)
    {
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ProbeTimeout);

        try
        {
            await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
            DatabaseHealthCheck database = scope.ServiceProvider.GetRequiredService<DatabaseHealthCheck>();
            HealthCheckResult databaseResult = await database.CheckHealthAsync(context, timeout.Token);
            if (databaseResult.Status != HealthStatus.Healthy)
            {
                return HealthCheckResult.Unhealthy("PostgreSQL is unavailable.");
            }

            HealthCheckResult redisResult = await _redis.CheckHealthAsync(context, timeout.Token);
            if (redisResult.Status != HealthStatus.Healthy)
            {
                return HealthCheckResult.Unhealthy("Redis is unavailable.");
            }

            HealthCheckResult storageResult = await _storage.CheckHealthAsync(context, timeout.Token);
            return storageResult.Status == HealthStatus.Healthy
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Degraded("Image storage is unavailable.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            return HealthCheckResult.Unhealthy("A readiness dependency is unavailable.");
        }
    }

    private sealed record CachedResult(HealthCheckResult Result, DateTimeOffset ExpiresAt);
}
