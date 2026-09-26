using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Kahoot.Infrastructure.Persistence;

internal sealed class DatabaseMigrationService : IHostedService
{
    private const long MigrationLockKey = 0x4B41484F4F545F4D;
    private const int DdlLockTimeoutSeconds = 5;
    private static readonly TimeSpan[] ConnectionRetryDelays =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(4),
        TimeSpan.FromSeconds(8),
        TimeSpan.FromSeconds(16)
    ];

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DatabaseOptions _databaseOptions;
    private readonly ILogger<DatabaseMigrationService> _logger;

    public DatabaseMigrationService(
        IServiceScopeFactory scopeFactory,
        IOptions<DatabaseOptions> databaseOptions,
        ILogger<DatabaseMigrationService> logger)
    {
        _scopeFactory = scopeFactory;
        _databaseOptions = databaseOptions.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        long startedAt = Stopwatch.GetTimestamp();
        _logger.LogInformation("Waiting to coordinate database migrations. EventName={EventName}", "DatabaseMigrationStarting");

        try
        {
            for (int attempt = 0; attempt <= ConnectionRetryDelays.Length; attempt++)
            {
                await using var connection = new NpgsqlConnection(_databaseOptions.ConnectionString);
                bool migrationStarted = false;

                try
                {
                    await connection.OpenAsync(cancellationToken);
                    await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
                    await using var command = connection.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandText = "SELECT pg_advisory_xact_lock(@lockKey)";
                    command.CommandTimeout = 0;
                    command.Parameters.AddWithValue("lockKey", MigrationLockKey);

                    await command.ExecuteNonQueryAsync(cancellationToken);
                    _logger.LogInformation("Database migration lock acquired. EventName={EventName}", "DatabaseMigrationLockAcquired");

                    migrationStarted = true;
                    await ApplyMigrationsAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);

                    _logger.LogInformation(
                        "Database migrations completed. EventName={EventName} ElapsedMilliseconds={ElapsedMilliseconds}",
                        "DatabaseMigrationCompleted",
                        Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
                    return;
                }
                catch (NpgsqlException exception) when (
                    !migrationStarted && exception.IsTransient && attempt < ConnectionRetryDelays.Length)
                {
                    TimeSpan delay = ConnectionRetryDelays[attempt];
                    _logger.LogWarning(
                        "PostgreSQL unavailable before migrations. EventName={EventName} Attempt={Attempt} RetryDelaySeconds={RetryDelaySeconds}",
                        "DatabaseMigrationConnectionRetry",
                        attempt + 1,
                        delay.TotalSeconds);
                    await Task.Delay(delay, cancellationToken);
                }
            }

            throw new InvalidOperationException("Database migration did not complete.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Database migrations failed; application startup is stopping. EventName={EventName} ElapsedMilliseconds={ElapsedMilliseconds}",
                "DatabaseMigrationFailed",
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task ApplyMigrationsAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.Database.SetCommandTimeout(_databaseOptions.MigrationCommandTimeoutSeconds);

        // Keep this session open so the setting applies to EF's history-table lock and every DDL command.
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            $"SET lock_timeout = '{DdlLockTimeoutSeconds}s'", cancellationToken);
        await dbContext.Database.MigrateAsync(cancellationToken);
    }
}
