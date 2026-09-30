using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kahoot.Infrastructure.Persistence;

internal sealed class RefreshTokenCleanupWorker : BackgroundService
{
    // ====================================================================================================
    // BACKPRESSURE PATTERN: Database Write Pacing & Cooperative Yielding (Chunked Batch Deletions)
    // ----------------------------------------------------------------------------------------------------
    // Context / Problem:
    // Large background batch deletions (e.g., millions of expired tokens) in a single database transaction
    // cause long-held row/table locks, PostgreSQL Write-Ahead Log (WAL) volume spikes, and replication lag,
    // which starves online interactive transactional traffic.
    //
    // Approach & Implementation:
    // 1. Chunked Bounded Batches (BatchSize = 500, MaxBatchesPerPass = 40): Deletes records in small 500-row
    //    chunks within short, isolated DbContext transactions (releasing locks immediately per batch).
    // 2. Cooperative Backpressure Yielding (BatchYieldInterval = 100ms): Between consecutive batch deletions,
    //    the background worker asynchronously yields execution (Task.Delay) for 100ms. This inserts deliberate
    //    pacing to allow PostgreSQL engine write queues and concurrent user transactions to process without contention.
    // ====================================================================================================
    // Bounded Batch Deletion - Deletes in small 500-row chunks with yielding to avoid lock escalation and WAL spikes
    private const int BatchSize = 500;
    private const int MaxBatchesPerPass = 40;
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(10);

    // Forensic Evidence Retention - Retains expired/revoked tokens for 7 days to detect delayed replay attacks
    private static readonly TimeSpan RetentionPeriod = TimeSpan.FromDays(7);
    private static readonly TimeSpan BatchYieldInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(60)
    ];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RefreshTokenCleanupWorker> _logger;

    public RefreshTokenCleanupWorker(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        ILogger<RefreshTokenCleanupWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    // Periodic Background Maintenance - PeriodicTimer prevents timer drift during scheduled background sweeps
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new PeriodicTimer(CleanupInterval, _timeProvider);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await ExecuteCleanupPassAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Application shutdown cancels the timer or the current cleanup pass.
        }
    }

    private async Task ExecuteCleanupPassAsync(CancellationToken stoppingToken)
    {
        for (int attempt = 0; attempt <= RetryDelays.Length; attempt++)
        {
            (bool success, Exception? error, int deletedCount, double elapsedMs) = await PerformCleanupAttemptAsync(stoppingToken);

            if (success)
            {
                if (deletedCount > 0)
                {
                    _logger.LogInformation(
                        "Refresh token cleanup completed. EventName={EventName} DeletedCount={DeletedCount} ElapsedMilliseconds={ElapsedMilliseconds} Attempt={Attempt}",
                        "RefreshTokenCleanupCompleted",
                        deletedCount,
                        elapsedMs,
                        attempt + 1);
                }
                else
                {
                    _logger.LogDebug(
                        "Refresh token cleanup completed with zero deleted tokens. EventName={EventName} DeletedCount={DeletedCount} ElapsedMilliseconds={ElapsedMilliseconds}",
                        "RefreshTokenCleanupCompleted",
                        0,
                        elapsedMs);
                }

                return;
            }

            if (attempt < RetryDelays.Length)
            {
                TimeSpan delay = RetryDelays[attempt];
                _logger.LogWarning(
                    error,
                    "Refresh token cleanup attempt failed; retrying. EventName={EventName} Attempt={Attempt} RetryDelaySeconds={RetryDelaySeconds} ElapsedMilliseconds={ElapsedMilliseconds}",
                    "RefreshTokenCleanupAttemptFailed",
                    attempt + 1,
                    delay.TotalSeconds,
                    elapsedMs);

                await Task.Delay(delay, _timeProvider, stoppingToken);
            }
            else
            {
                _logger.LogError(
                    error,
                    "Refresh token cleanup failed and exhausted all retry attempts. EventName={EventName} Attempts={Attempts} ElapsedMilliseconds={ElapsedMilliseconds}",
                    "RefreshTokenCleanupFailed",
                    attempt + 1,
                    elapsedMs);
            }
        }
    }

    private async Task<(bool Success, Exception? Error, int DeletedCount, double ElapsedMs)> PerformCleanupAttemptAsync(CancellationToken cancellationToken)
    {
        int deletedCount = 0;
        long startedAt = _timeProvider.GetTimestamp();
        DateTimeOffset cutoff = _timeProvider.GetUtcNow() - RetentionPeriod;

        try
        {
            for (int batchNumber = 0; batchNumber < MaxBatchesPerPass; batchNumber++)
            {
                int batchDeletedCount;
                // Async Service Scope - Resolves scoped DbContext per batch to avoid concurrency bugs and memory leaks in singleton worker
                await using (AsyncServiceScope scope = _scopeFactory.CreateAsyncScope())
                {
                    AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    batchDeletedCount = await DeleteBatchAsync(dbContext, cutoff, cancellationToken);
                }

                deletedCount += batchDeletedCount;

                if (batchDeletedCount < BatchSize)
                {
                    break;
                }

                if (batchNumber < MaxBatchesPerPass - 1)
                {
                    await Task.Delay(BatchYieldInterval, _timeProvider, cancellationToken);
                }
            }

            double elapsedMs = _timeProvider.GetElapsedTime(startedAt).TotalMilliseconds;
            return (true, null, deletedCount, elapsedMs);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            double elapsedMs = _timeProvider.GetElapsedTime(startedAt).TotalMilliseconds;
            return (false, exception, deletedCount, elapsedMs);
        }
    }

    // Non-Blocking Multi-Replica Batching (SKIP LOCKED) - LIMIT 500 batches and skips locked rows so replicas do not compete or deadlock
    private static Task<int> DeleteBatchAsync(
        AppDbContext dbContext,
        DateTimeOffset cutoff,
        CancellationToken cancellationToken)
    {
        // Both expiration and revocation must be past the cutoff; rotation alone is not eligible.
        // Query Performance & Concurrency: Raw SQL CTE with LIMIT 500 and FOR UPDATE SKIP LOCKED ensures bounded batch execution without lock escalation or multi-replica deadlocks
        return dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            WITH candidates AS MATERIALIZED (
                SELECT id
                FROM refresh_tokens
                WHERE expires_at <= {cutoff}
                  AND (revoked_at IS NULL OR revoked_at <= {cutoff})
                ORDER BY expires_at
                LIMIT {BatchSize}
                FOR UPDATE SKIP LOCKED
            )
            DELETE FROM refresh_tokens AS token
            USING candidates
            WHERE token.id = candidates.id
            """, cancellationToken);
    }
}
