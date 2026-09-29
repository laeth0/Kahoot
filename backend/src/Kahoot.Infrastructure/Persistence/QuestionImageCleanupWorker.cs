using Kahoot.Application.Common.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kahoot.Infrastructure.Persistence;

internal sealed class QuestionImageCleanupWorker : BackgroundService
{
    private static readonly TimeSpan BatchYieldInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(60)
    ];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ImageStorageOptions _options;
    private readonly ILogger<QuestionImageCleanupWorker> _logger;
    private IEnumerator<FileInfo>? _reconciliationEnumerator;

    public QuestionImageCleanupWorker(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        IHostEnvironment hostEnvironment,
        IOptions<ImageStorageOptions> options,
        ILogger<QuestionImageCleanupWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _hostEnvironment = hostEnvironment;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TimeSpan interval = TimeSpan.FromMinutes(_options.CleanupIntervalMinutes);
        using PeriodicTimer timer = new PeriodicTimer(interval, _timeProvider);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await ExecuteFullCleanupPassAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Application shutdown cancels the periodic timer or current pass.
        }
        finally
        {
            _reconciliationEnumerator?.Dispose();
        }
    }

    private async Task ExecuteFullCleanupPassAsync(CancellationToken stoppingToken)
    {
        string uploadsDir = GetUploadsDirectory();
        string stagingDir = GetStagingDirectory();

        await ExecuteDatabaseOrphanCleanupWithRetriesAsync(uploadsDir, stoppingToken);

        DateTimeOffset stagingCutoff = _timeProvider.GetUtcNow() - TimeSpan.FromHours(_options.StagingQuarantineHours);
        SweepStagingQuarantine(stagingDir, stagingCutoff);

        DateTimeOffset orphanCutoff = _timeProvider.GetUtcNow() - TimeSpan.FromDays(_options.OrphanRetentionDays);
        await ExecuteFilesystemReconciliationPassAsync(uploadsDir, orphanCutoff, stoppingToken);
    }

    private async Task ExecuteDatabaseOrphanCleanupWithRetriesAsync(string uploadsDir, CancellationToken stoppingToken)
    {
        for (int attempt = 0; attempt <= RetryDelays.Length; attempt++)
        {
            (bool success, Exception? error, int deletedCount, double elapsedMs) = await PerformDatabaseCleanupAttemptAsync(uploadsDir, stoppingToken);

            if (success)
            {
                if (deletedCount > 0)
                {
                    _logger.LogInformation(
                        "Question image orphan cleanup completed. EventName={EventName} DeletedCount={DeletedCount} ElapsedMilliseconds={ElapsedMilliseconds} Attempt={Attempt}",
                        "QuestionImageCleanupCompleted",
                        deletedCount,
                        elapsedMs,
                        attempt + 1);
                }
                else
                {
                    _logger.LogDebug(
                        "Question image orphan cleanup completed with zero deleted images. EventName={EventName} DeletedCount={DeletedCount} ElapsedMilliseconds={ElapsedMilliseconds}",
                        "QuestionImageCleanupCompleted",
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
                    "Question image orphan cleanup attempt failed; retrying. EventName={EventName} Attempt={Attempt} RetryDelaySeconds={RetryDelaySeconds} ElapsedMilliseconds={ElapsedMilliseconds}",
                    "QuestionImageCleanupAttemptFailed",
                    attempt + 1,
                    delay.TotalSeconds,
                    elapsedMs);

                await Task.Delay(delay, _timeProvider, stoppingToken);
            }
            else
            {
                _logger.LogError(
                    error,
                    "Question image orphan cleanup failed and exhausted all retry attempts. EventName={EventName} Attempts={Attempts} ElapsedMilliseconds={ElapsedMilliseconds}",
                    "QuestionImageCleanupFailed",
                    attempt + 1,
                    elapsedMs);
            }
        }
    }

    private async Task<(bool Success, Exception? Error, int DeletedCount, double ElapsedMs)> PerformDatabaseCleanupAttemptAsync(
        string uploadsDir,
        CancellationToken cancellationToken)
    {
        int deletedCount = 0;
        long startedAt = _timeProvider.GetTimestamp();
        DateTimeOffset cutoff = _timeProvider.GetUtcNow() - TimeSpan.FromDays(_options.OrphanRetentionDays);

        try
        {
            for (int batchNumber = 0; batchNumber < _options.MaxBatchesPerPass; batchNumber++)
            {
                List<string> batchDeletedPaths;
                await using (AsyncServiceScope scope = _scopeFactory.CreateAsyncScope())
                {
                    AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    batchDeletedPaths = await DeleteDatabaseOrphanBatchAsync(dbContext, cutoff, _options.CleanupBatchSize, cancellationToken);
                }

                foreach (string storagePath in batchDeletedPaths)
                {
                    UnlinkPhysicalFile(uploadsDir, storagePath);
                }

                deletedCount += batchDeletedPaths.Count;

                if (batchDeletedPaths.Count < _options.CleanupBatchSize)
                {
                    break;
                }

                if (batchNumber < _options.MaxBatchesPerPass - 1)
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

    // Atomic CTE Batch Deletion (IMG-CLEAN-001) - Deletes orphaned images using MATERIALIZED CTE with FOR UPDATE SKIP LOCKED
    private static async Task<List<string>> DeleteDatabaseOrphanBatchAsync(
        AppDbContext dbContext,
        DateTimeOffset cutoff,
        int batchSize,
        CancellationToken cancellationToken)
    {
        // High-Concurrency Worker Batch - Locks eligible rows and deletes them atomically in a single statement
        return await dbContext.Database.SqlQuery<string>($"""
            WITH candidates AS MATERIALIZED (
                SELECT qi.id, qi.storage_path
                FROM question_images qi
                WHERE qi.unreferenced_since IS NOT NULL
                  AND qi.unreferenced_since <= {cutoff}
                  AND NOT EXISTS (SELECT 1 FROM questions q WHERE q.image_id = qi.id)
                  AND NOT EXISTS (SELECT 1 FROM game_question_snapshots gqs WHERE gqs.image_id = qi.id)
                ORDER BY qi.unreferenced_since
                LIMIT {batchSize}
                FOR UPDATE SKIP LOCKED
            ),
            deleted AS (
                DELETE FROM question_images qi
                USING candidates
                WHERE qi.id = candidates.id
                RETURNING qi.storage_path
            )
            SELECT storage_path AS "Value" FROM deleted;
            """)
            .ToListAsync(cancellationToken);
    }

    private void UnlinkPhysicalFile(string uploadsDir, string storagePath)
    {
        try
        {
            string? physicalPath = ResolvePhysicalPath(uploadsDir, storagePath);
            if (physicalPath is not null && File.Exists(physicalPath))
            {
                File.Delete(physicalPath);
                _logger.LogInformation(
                    "Orphan image file unlinked. EventName={EventName} StoragePath={StoragePath} PhysicalPath={PhysicalPath}",
                    "OrphanImageFileUnlinked",
                    storagePath,
                    physicalPath);
            }
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Failed to unlink physical file after database deletion; will be collected by filesystem reconciliation. EventName={EventName} StoragePath={StoragePath}",
                "OrphanImageFileUnlinkFailed",
                storagePath);
        }
    }

    private void SweepStagingQuarantine(string stagingDir, DateTimeOffset stagingCutoff)
    {
        if (!Directory.Exists(stagingDir))
        {
            return;
        }

        try
        {
            DirectoryInfo directoryInfo = new DirectoryInfo(stagingDir);
            foreach (FileInfo file in directoryInfo.EnumerateFiles("*.tmp", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    if (file.LastWriteTimeUtc <= stagingCutoff.UtcDateTime)
                    {
                        file.Delete();
                        _logger.LogInformation(
                            "Staging quarantine sweep deleted stale upload file. EventName={EventName} FilePath={FilePath}",
                            "StagingUploadCleaned",
                            file.FullName);
                    }
                }
                catch (Exception fileException)
                {
                    _logger.LogWarning(
                        fileException,
                        "Failed to delete stale staging file during quarantine sweep. EventName={EventName} FilePath={FilePath}",
                        "StagingFileDeleteFailed",
                        file.FullName);
                }
            }
        }
        catch (Exception sweepException)
        {
            _logger.LogWarning(
                sweepException,
                "Error occurred during staging quarantine sweep. EventName={EventName}",
                "StagingQuarantineSweepFailed");
        }
    }

    private async Task ExecuteFilesystemReconciliationPassAsync(
        string uploadsDir,
        DateTimeOffset orphanCutoff,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(uploadsDir))
        {
            _reconciliationEnumerator?.Dispose();
            _reconciliationEnumerator = null;
            return;
        }

        try
        {
            List<FileInfo> candidateFiles = new List<FileInfo>();
            int maxFilesInspected = _options.ReconciliationBatchSize * _options.MaxBatchesPerPass;

            for (int inspected = 0; inspected < maxFilesInspected; inspected++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                _reconciliationEnumerator ??= new DirectoryInfo(uploadsDir)
                    .EnumerateFiles("*", SearchOption.TopDirectoryOnly)
                    .GetEnumerator();

                if (!_reconciliationEnumerator.MoveNext())
                {
                    _reconciliationEnumerator.Dispose();
                    _reconciliationEnumerator = null;
                    break;
                }

                FileInfo file = _reconciliationEnumerator.Current;

                string extension = file.Extension;
                if (!extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) &&
                    !extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) &&
                    !extension.Equals(".png", StringComparison.OrdinalIgnoreCase) &&
                    !extension.Equals(".webp", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (file.LastWriteTimeUtc <= orphanCutoff.UtcDateTime)
                {
                    candidateFiles.Add(file);
                    if (candidateFiles.Count >= _options.ReconciliationBatchSize)
                    {
                        await ReconcileBatchAsync(candidateFiles, cancellationToken);
                        candidateFiles.Clear();
                    }
                }
            }

            if (candidateFiles.Count > 0)
            {
                await ReconcileBatchAsync(candidateFiles, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _reconciliationEnumerator?.Dispose();
            _reconciliationEnumerator = null;
            _logger.LogWarning(
                exception,
                "Error occurred during filesystem reconciliation pass. EventName={EventName}",
                "FilesystemReconciliationFailed");
        }
    }

    private async Task ReconcileBatchAsync(
        List<FileInfo> candidateFiles,
        CancellationToken cancellationToken)
    {
        Dictionary<string, FileInfo> pathToCandidate = new Dictionary<string, FileInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (FileInfo candidate in candidateFiles)
        {
            string relativePath = $"/uploads/{candidate.Name}";
            pathToCandidate[relativePath] = candidate;
        }

        List<string> candidatePaths = pathToCandidate.Keys.ToList();

        List<string> existingPaths;
        await using (AsyncServiceScope scope = _scopeFactory.CreateAsyncScope())
        {
            AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            existingPaths = await dbContext.QuestionImages
                .AsNoTracking()
                .Where(image => candidatePaths.Contains(image.StoragePath))
                .Select(image => image.StoragePath)
                .ToListAsync(cancellationToken);
        }

        HashSet<string> existingPathSet = new HashSet<string>(existingPaths, StringComparer.OrdinalIgnoreCase);

        foreach (KeyValuePair<string, FileInfo> pair in pathToCandidate)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!existingPathSet.Contains(pair.Key))
            {
                try
                {
                    if (pair.Value.Exists)
                    {
                        pair.Value.Delete();
                        _logger.LogInformation(
                            "Filesystem reconciliation unlinked orphan file. EventName={EventName} FilePath={FilePath} StoragePath={StoragePath}",
                            "OrphanImageFileReconciled",
                            pair.Value.FullName,
                            pair.Key);
                    }
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(
                        exception,
                        "Failed to delete reconciled orphan file. EventName={EventName} FilePath={FilePath}",
                        "ReconciledFileDeleteFailed",
                        pair.Value.FullName);
                }
            }
        }
    }

    private string? ResolvePhysicalPath(string uploadsDir, string storagePath)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            return null;
        }

        string normalized = storagePath.Trim().Replace('\\', '/');
        if (!normalized.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        string fileName = normalized["/uploads/".Length..];
        if (fileName.Contains("..") || fileName.Contains('/') || fileName.Contains('\\'))
        {
            return null;
        }

        return Path.Combine(uploadsDir, fileName);
    }

    private string GetUploadsDirectory()
    {
        string webRoot = Path.Combine(_hostEnvironment.ContentRootPath, "wwwroot");
        return Path.Combine(webRoot, _options.UploadsSubdirectory);
    }

    private string GetStagingDirectory()
    {
        string webRoot = Path.Combine(_hostEnvironment.ContentRootPath, "wwwroot");
        return Path.Combine(webRoot, _options.StagingSubdirectory);
    }
}
