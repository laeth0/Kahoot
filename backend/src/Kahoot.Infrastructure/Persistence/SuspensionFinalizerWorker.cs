using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Features.Games;
using Kahoot.Application.Features.Games.Models;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kahoot.Infrastructure.Persistence;

internal sealed class SuspensionFinalizerWorker : BackgroundService
{
    private const int GameBatchSize = 10;
    private const int HostScanBatchSize = 50;
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISuspensionFinalizerChannel _channel;
    private readonly ICriticalWorkerFailureTracker _failureTracker;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SuspensionFinalizerWorker> _logger;
    private bool _hostBatchFailed;

    public SuspensionFinalizerWorker(
        IServiceScopeFactory scopeFactory,
        ISuspensionFinalizerChannel channel,
        ICriticalWorkerFailureTracker failureTracker,
        TimeProvider timeProvider,
        ILogger<SuspensionFinalizerWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _channel = channel;
        _failureTracker = failureTracker;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Suspension finalizer worker starting. Executing startup sweep. EventName={EventName}", "SuspensionFinalizerStarting");
        await TrySweepAsync(stoppingToken);

        using PeriodicTimer timer = new PeriodicTimer(SweepInterval, _timeProvider);
        Task<bool> channelWait = _channel.WaitToReadAsync(stoppingToken).AsTask();
        Task<bool> timerWait = timer.WaitForNextTickAsync(stoppingToken).AsTask();

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                Task<bool> completedWait = timerWait.IsCompleted
                    ? timerWait
                    : await Task.WhenAny(timerWait, channelWait);

                if (completedWait == timerWait)
                {
                    if (!await timerWait)
                    {
                        break;
                    }

                    timerWait = timer.WaitForNextTickAsync(stoppingToken).AsTask();
                    await TrySweepAsync(stoppingToken);
                    continue;
                }

                if (!await channelWait)
                {
                    break;
                }

                channelWait = _channel.WaitToReadAsync(stoppingToken).AsTask();
                int processedHosts = 0;
                while (processedHosts < HostScanBatchSize && _channel.TryRead(out Guid hostAccountId))
                {
                    await TryFinalizeHostBatchAsync(hostAccountId, stoppingToken);
                    processedHosts++;
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal application shutdown.
        }
    }

    private async Task TrySweepAsync(CancellationToken cancellationToken)
    {
        try
        {
            _hostBatchFailed = false;
            await ProcessAllPendingSuspensionsAsync(cancellationToken);
            if (!_hostBatchFailed)
            {
                _failureTracker.ReportSuccess("SuspensionGameFinalizer");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Critical Worker Failure Escalation (OPS-WORK-002) - Escalates error tracking if suspension sweep fails repeatedly beyond 15 minutes
            _failureTracker.ReportFailure("SuspensionGameFinalizer", exception);
            _logger.LogError(exception, "Suspension finalization sweep failed. EventName={EventName}", "SuspensionFinalizerSweepError");
        }
    }

    private async Task ProcessAllPendingSuspensionsAsync(CancellationToken cancellationToken)
    {
        Guid lastHostId = Guid.Empty;

        while (!cancellationToken.IsCancellationRequested)
        {
            List<Guid> pendingHostIds;
            await using (AsyncServiceScope scope = _scopeFactory.CreateAsyncScope())
            {
                AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                pendingHostIds = await dbContext.Database.SqlQuery<Guid>(
                        $"""SELECT id AS "Value" FROM users WHERE role = {UserRole.Host} AND status = {UserStatus.Suspended} AND termination_pending AND id > {lastHostId} ORDER BY id LIMIT {HostScanBatchSize}""")
                    .ToListAsync(cancellationToken);
            }

            foreach (Guid hostId in pendingHostIds)
            {
                await TryFinalizeHostBatchAsync(hostId, cancellationToken);
            }

            if (pendingHostIds.Count < HostScanBatchSize)
            {
                break;
            }

            lastHostId = pendingHostIds[^1];
        }
    }

    private async Task TryFinalizeHostBatchAsync(Guid hostAccountId, CancellationToken cancellationToken)
    {
        try
        {
            if (await FinalizeHostBatchAsync(hostAccountId, cancellationToken))
            {
                _channel.NotifySuspension(hostAccountId);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _hostBatchFailed = true;
            _failureTracker.ReportFailure("SuspensionGameFinalizer", exception);
            _logger.LogError(exception, "Suspension finalization failed for host {HostAccountId}. EventName={EventName}",
                hostAccountId, "SuspensionFinalizerHostError");
        }
    }

    private async Task<bool> FinalizeHostBatchAsync(Guid hostAccountId, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Multi-Replica Host Claim (ADMIN-SUSP-002) - Claims suspended host row with 'FOR UPDATE SKIP LOCKED' to avoid concurrent worker conflicts
        List<User> hosts = await dbContext.Users
            .FromSqlInterpolated($"SELECT * FROM users WHERE id = {hostAccountId} FOR UPDATE SKIP LOCKED")
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        User? host = hosts.Count == 0 ? null : hosts[0];
        if (host is null || host.Role != UserRole.Host ||
            host.Status != UserStatus.Suspended || !host.TerminationPending)
        {
            return false;
        }

        // Bounded Game Batch Lock - Locks up to 10 active games owned by the suspended host for transactional termination
        List<Game> batchGames = await dbContext.Games
            .FromSqlInterpolated($"SELECT * FROM games WHERE host_account_id = {hostAccountId} AND status <> {GameStatus.Finished} ORDER BY created_at, id LIMIT {GameBatchSize} FOR UPDATE")
            .ToListAsync(cancellationToken);

        if (batchGames.Count == 0)
        {
            await ClearPendingAsync(dbContext, hostAccountId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            _logger.LogInformation("Completed suspension game finalization for host {HostAccountId}. EventName={EventName}",
                hostAccountId, "SuspensionFinalizationCompleted");
            return false;
        }

        Guid[] gameIds = batchGames.Select(game => game.Id).ToArray();

        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            WITH ranked AS (
                SELECT id, row_number() OVER (
                    PARTITION BY game_id
                    ORDER BY total_score DESC, normalized_nickname COLLATE "C" ASC, id ASC
                ) AS final_rank
                FROM participants
                WHERE game_id = ANY ({gameIds}) AND NOT is_removed
            )
            UPDATE participants AS participant
            SET rank = ranked.final_rank::integer
            FROM ranked
            WHERE participant.id = ranked.id
            """, cancellationToken);

        await dbContext.Participants
            .Where(participant => gameIds.Contains(participant.GameId) && participant.IsRemoved)
            .ExecuteUpdateAsync(setter => setter.SetProperty(participant => participant.Rank, (int?)null), cancellationToken);

        foreach (Game game in batchGames)
        {
            if (game.Status == GameStatus.QuestionActive)
            {
                await QuestionResultsMaterializer.MaterializeAsync(dbContext, game, host.UpdatedAt, cancellationToken);
            }

            game.Status = GameStatus.Finished;
            game.FinishedAt = host.UpdatedAt;
            game.HostGraceExpiresAt = null;
            game.Pin = null;
            game.IsTerminatedBySuspension = true;
            game.StateVersion += 1;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        List<Participant> podiumParticipants = await dbContext.Participants.AsNoTracking()
            .Where(participant => gameIds.Contains(participant.GameId) &&
                                  !participant.IsRemoved && participant.Rank <= 3)
            .OrderBy(participant => participant.GameId)
            .ThenBy(participant => participant.Rank)
            .ToListAsync(cancellationToken);

        Dictionary<Guid, List<PodiumParticipantDto>> podiumByGame = podiumParticipants
            .GroupBy(participant => participant.GameId)
            .ToDictionary(group => group.Key,
                group => group.Select(participant => new PodiumParticipantDto(
                    participant.Rank!.Value, participant.DisplayNickname, participant.TotalScore)).ToList());

        bool hasMoreGames = await dbContext.Games
            .AnyAsync(game => game.HostAccountId == hostAccountId && game.Status != GameStatus.Finished, cancellationToken);

        if (!hasMoreGames)
        {
            await ClearPendingAsync(dbContext, hostAccountId, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        IGameNotificationService notificationService = scope.ServiceProvider.GetRequiredService<IGameNotificationService>();
        foreach (Game game in batchGames)
        {
            object payload = new
            {
                gameId = game.Id,
                status = "FINISHED",
                stateVersion = game.StateVersion,
                finishedAt = game.FinishedAt,
                podium = podiumByGame.GetValueOrDefault(game.Id) ?? new List<PodiumParticipantDto>()
            };

            await notificationService.PublishGameEndedAsync(
                hostAccountId, game.Id, game.StateVersion, payload, CancellationToken.None);
        }

        if (!hasMoreGames)
        {
            _logger.LogInformation("Completed suspension game finalization for host {HostAccountId}. EventName={EventName}",
                hostAccountId, "SuspensionFinalizationCompleted");
        }

        return hasMoreGames;
    }

    private static async Task ClearPendingAsync(
        AppDbContext dbContext,
        Guid hostAccountId,
        CancellationToken cancellationToken)
    {
        await dbContext.Users
            .Where(user => user.Id == hostAccountId && user.TerminationPending)
            .ExecuteUpdateAsync(setter => setter.SetProperty(user => user.TerminationPending, false), cancellationToken);
    }
}
