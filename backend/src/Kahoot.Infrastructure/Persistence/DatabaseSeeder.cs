using System.Diagnostics;
using Kahoot.Application.Common.Seeding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Kahoot.Infrastructure.Persistence;

internal sealed class DatabaseSeeder : IHostedService
{
    private const long SeedingLockKey = 0x4B41484F4F545F53;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DatabaseOptions _databaseOptions;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(
        IServiceScopeFactory scopeFactory,
        IOptions<DatabaseOptions> databaseOptions,
        ILogger<DatabaseSeeder> logger)
    {
        _scopeFactory = scopeFactory;
        _databaseOptions = databaseOptions.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        long startedAt = Stopwatch.GetTimestamp();
        _logger.LogInformation("Waiting to coordinate database seeding. EventName={EventName}", "DatabaseSeedingStarting");

        try
        {
            await using NpgsqlConnection connection = new NpgsqlConnection(_databaseOptions.ConnectionString);
            await connection.OpenAsync(cancellationToken);

            await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using NpgsqlCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT pg_advisory_xact_lock(@lockKey)";
            command.CommandTimeout = 0;
            command.Parameters.AddWithValue("lockKey", SeedingLockKey);

            await command.ExecuteNonQueryAsync(cancellationToken);
            _logger.LogInformation("Database seeding lock acquired. EventName={EventName}", "DatabaseSeedingLockAcquired");

            await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
            IEnumerable<ISeeder> seeders = scope.ServiceProvider.GetServices<ISeeder>();

            foreach (ISeeder seeder in seeders)
            {
                await seeder.SeedAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Database seeding completed. EventName={EventName} ElapsedMilliseconds={ElapsedMilliseconds}",
                "DatabaseSeedingCompleted",
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Database seeding failed; application startup is stopping. EventName={EventName} ElapsedMilliseconds={ElapsedMilliseconds}",
                "DatabaseSeedingFailed",
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
