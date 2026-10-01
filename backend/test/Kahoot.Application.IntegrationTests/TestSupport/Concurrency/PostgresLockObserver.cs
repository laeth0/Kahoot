namespace Kahoot.Application.IntegrationTests.TestSupport.Concurrency;

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Kahoot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

public static class PostgresLockObserver
{
    public static async Task<int> GetBackendPidAsync(AppDbContext dbContext, CancellationToken cancellationToken = default)
    {
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        DbConnection connection = dbContext.Database.GetDbConnection();
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = "SELECT pg_backend_pid()";
        object? result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result);
    }

    public static async Task WaitForBlockerAsync(
        string connectionString,
        int blockedPid,
        int expectedBlockerPid,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        await using NpgsqlConnection connection = new(connectionString);
        await connection.OpenAsync(timeoutCts.Token);

        while (!timeoutCts.Token.IsCancellationRequested)
        {
            await using NpgsqlCommand command = connection.CreateCommand();
            command.CommandText = "SELECT unnest(pg_blocking_pids(@blockedPid))";
            command.Parameters.AddWithValue("blockedPid", blockedPid);

            await using DbDataReader reader = await command.ExecuteReaderAsync(timeoutCts.Token);
            List<int> blockingPids = new();
            while (await reader.ReadAsync(timeoutCts.Token))
            {
                blockingPids.Add(reader.GetInt32(0));
            }

            if (blockingPids.Contains(expectedBlockerPid))
            {
                return;
            }

            await Task.Delay(25, timeoutCts.Token);
        }

        throw new TimeoutException($"Timed out waiting for PID {blockedPid} to be blocked by PID {expectedBlockerPid}.");
    }
}
