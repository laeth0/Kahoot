using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kahoot.Infrastructure.Persistence;

internal sealed class RefreshTokenCleanupWorker : BackgroundService
{
    private const int BatchSize = 500;
    private const int MaxBatchesPerPass = 40;
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(10);
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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new PeriodicTimer(CleanupInterval, _timeProvider);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                for (int attempt = 0; attempt <= RetryDelays.Length; attempt++)
                {
                    if (await TryCleanupAsync(stoppingToken))
                    {
                        break;
                    }

                    if (attempt < RetryDelays.Length)
                    {
                        await Task.Delay(RetryDelays[attempt], _timeProvider, stoppingToken);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Application shutdown cancels the timer or the current cleanup pass.
        }
    }

    private async Task<bool> TryCleanupAsync(CancellationToken cancellationToken)
    {
        int deletedCount = 0;
        long startedAt = _timeProvider.GetTimestamp();
        DateTimeOffset cutoff = _timeProvider.GetUtcNow() - RetentionPeriod;

        try
        {
            for (int batchNumber = 0; batchNumber < MaxBatchesPerPass; batchNumber++)
            {
                int batchDeletedCount;
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

            _logger.LogInformation(
                "Refresh token cleanup completed. EventName={EventName} DeletedCount={DeletedCount} ElapsedMilliseconds={ElapsedMilliseconds}",
                "RefreshTokenCleanupCompleted",
                deletedCount,
                _timeProvider.GetElapsedTime(startedAt).TotalMilliseconds);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Refresh token cleanup failed. EventName={EventName} DeletedCount={DeletedCount} ElapsedMilliseconds={ElapsedMilliseconds}",
                "RefreshTokenCleanupFailed",
                deletedCount,
                _timeProvider.GetElapsedTime(startedAt).TotalMilliseconds);
            return false;
        }
    }

    private static Task<int> DeleteBatchAsync(
        AppDbContext dbContext,
        DateTimeOffset cutoff,
        CancellationToken cancellationToken)
    {
        // Both expiration and revocation must be past the cutoff; rotation alone is not eligible.
        // One statement is one atomic transaction. Locked candidates are skipped by other replicas.
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
