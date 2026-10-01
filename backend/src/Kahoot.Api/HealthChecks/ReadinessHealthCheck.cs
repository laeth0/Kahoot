namespace Kahoot.Api.HealthChecks;

using Kahoot.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;

// Readiness Health Check - Aggregates application lifecycle, database, redis, and storage probes with short TTL caching.
internal sealed class ReadinessHealthCheck : IHealthCheck
{
    // Health Cache TTL - Dampens thundering herd load from rapid load balancer / orchestrator polling intervals.
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(3);
    // Probe Timeout - Bounds per-dependency probe execution to 450ms to fail fast before reverse proxy timeouts trigger.
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromMilliseconds(450);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RedisHealthCheck _redis;
    private readonly StorageHealthCheck _storage;
    private readonly ICriticalWorkerFailureTracker _workerFailureTracker;
    private readonly TimeProvider _timeProvider;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly SemaphoreSlim _refreshGate = new SemaphoreSlim(1, 1);
    private CachedResult? _cachedResult;

    public ReadinessHealthCheck(
        IServiceScopeFactory scopeFactory,
        RedisHealthCheck redis,
        StorageHealthCheck storage,
        ICriticalWorkerFailureTracker workerFailureTracker,
        TimeProvider timeProvider,
        IHostApplicationLifetime lifetime)
    {
        _scopeFactory = scopeFactory;
        _redis = redis;
        _storage = storage;
        _workerFailureTracker = workerFailureTracker;
        _timeProvider = timeProvider;
        _lifetime = lifetime;
    }

    // Health Evaluation - Validates lifecycle state and returns cached result or triggers serialized dependency probes.
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        // Traffic Drain Guard - Immediately reports unhealthy if server is shutting down to remove pod from balancer routing.
        if (!IsAcceptingTraffic())
        {
            return HealthCheckResult.Unhealthy("Application is not accepting traffic.");
        }

        // Cache Fast Path - Returns volatile cached result if still within 3-second TTL window.
        CachedResult? cached = Volatile.Read(ref _cachedResult);
        if (cached is not null && _timeProvider.GetUtcNow() < cached.ExpiresAt)
        {
            return IsAcceptingTraffic()
                ? cached.Result
                : HealthCheckResult.Unhealthy("Application is not accepting traffic.");
        }

        // Serialized Refresh - Acquires semaphore to prevent redundant concurrent probe calls against backing infrastructure.
        if (!await _refreshGate.WaitAsync(ProbeTimeout, cancellationToken))
        {
            return HealthCheckResult.Unhealthy("Readiness probe is busy.");
        }

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

    // Host Lifecycle Gate - Verifies host runtime has completed initialization and has not initiated SIGTERM shutdown.
    private bool IsAcceptingTraffic() =>
        _lifetime.ApplicationStarted.IsCancellationRequested &&
        !_lifetime.ApplicationStopping.IsCancellationRequested;

    // Dependency Probe Pipeline - Concurrently probes backing databases and degrades gracefully if non-critical subsystems fail.
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
            if (storageResult.Status != HealthStatus.Healthy)
            {
                return HealthCheckResult.Degraded("Image storage is unavailable.");
            }

            // Critical Worker Backlog Escalation (OPS-WORK-002) - Marks readiness degraded if a critical finalization worker backlog persists beyond 15 minutes
            if (_workerFailureTracker.HasDegradedBacklog(out string? failingWorker, out TimeSpan? duration))
            {
                return HealthCheckResult.Degraded($"Critical worker {failingWorker} backlog has persisted beyond 15 minutes.");
            }

            return HealthCheckResult.Healthy();
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
