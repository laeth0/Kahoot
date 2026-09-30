namespace Kahoot.Infrastructure.Persistence;

using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Features.Games;
using Kahoot.Application.Features.Games.Models;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Kahoot.Infrastructure.Realtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

internal sealed class GameAbandonmentWorker : BackgroundService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly HostPresenceService _presence;
    private readonly ICriticalWorkerFailureTracker _failureTracker;
    private readonly ILogger<GameAbandonmentWorker> _logger;
    private Guid _lastRecoveryGameId;

    public GameAbandonmentWorker(
        IServiceScopeFactory scopeFactory,
        HostPresenceService presence,
        ICriticalWorkerFailureTracker failureTracker,
        TimeProvider timeProvider,
        ILogger<GameAbandonmentWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _presence = presence;
        _failureTracker = failureTracker;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new PeriodicTimer(SweepInterval, _timeProvider);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                bool recoverySucceeded = await RecoverMissingGraceTimersAsync(stoppingToken);
                await SweepAbandonedGamesAsync(recoverySucceeded, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Graceful shutdown
        }
    }

    private async Task<bool> RecoverMissingGraceTimersAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
            AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            List<Guid> gameIds = await dbContext.Database.SqlQuery<Guid>($"""
                    SELECT id AS "Value" FROM games
                    WHERE status <> {GameStatus.Finished}
                      AND host_grace_expires_at IS NULL AND id > {_lastRecoveryGameId}
                    ORDER BY id LIMIT {BatchSize}
                    """)
                .ToListAsync(cancellationToken);

            if (gameIds.Count == 0)
            {
                _lastRecoveryGameId = Guid.Empty;
                return true;
            }

            _lastRecoveryGameId = gameIds[^1];
            foreach (Guid gameId in gameIds)
            {
                if (await _presence.HasAnyAsync(gameId))
                {
                    continue;
                }

                await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
                List<Game> lockedGames = await dbContext.Games
                    .FromSqlInterpolated($"SELECT * FROM games WHERE id = {gameId} FOR UPDATE")
                    .ToListAsync(cancellationToken);
                Game? game = lockedGames.Count == 0 ? null : lockedGames[0];
                if (game is not null && game.Status != GameStatus.Finished &&
                    game.HostGraceExpiresAt is null && !await _presence.HasAnyAsync(gameId))
                {
                    game.HostGraceExpiresAt = _timeProvider.GetUtcNow().AddSeconds(300);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    _logger.LogWarning("Recovered abandonment grace after Host presence lease expired. GameId={GameId} ExpiresAt={ExpiresAt}",
                        gameId, game.HostGraceExpiresAt);
                }

                await transaction.CommitAsync(cancellationToken);
            }

            if (gameIds.Count < BatchSize)
            {
                _lastRecoveryGameId = Guid.Empty;
            }

            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _failureTracker.ReportFailure("AbandonedGameFinalizer", exception);
            _logger.LogError(exception, "Abandonment grace recovery failed.");
            return false;
        }
    }

    private async Task SweepAbandonedGamesAsync(bool recoverySucceeded, CancellationToken cancellationToken)
    {
        try
        {
            DateTimeOffset utcNow = _timeProvider.GetUtcNow();

            await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
            AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            IGameNotificationService notificationService = scope.ServiceProvider.GetRequiredService<IGameNotificationService>();

            await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            // High-Concurrency Worker Sweep (SCALE-WORK-001) - Claims expired games using 'FOR UPDATE SKIP LOCKED' across replica workers
            List<Game> candidateGames = await dbContext.Games
                .FromSqlInterpolated($"""
                    SELECT * FROM games
                    WHERE status <> {GameStatus.Finished}
                      AND host_grace_expires_at IS NOT NULL
                      AND host_grace_expires_at <= {utcNow}
                    ORDER BY host_grace_expires_at
                    LIMIT {BatchSize}
                    FOR UPDATE SKIP LOCKED
                    """)
                .ToListAsync(cancellationToken);

            if (candidateGames.Count == 0)
            {
                if (recoverySucceeded)
                {
                    _failureTracker.ReportSuccess("AbandonedGameFinalizer");
                }
                return;
            }

            List<Game> abandonedGames = new(candidateGames.Count);
            foreach (Game game in candidateGames)
            {
                if (await _presence.HasAnyAsync(game.Id))
                {
                    game.HostGraceExpiresAt = null;
                    continue;
                }

                if (game.Status == GameStatus.QuestionActive)
                {
                    await QuestionResultsMaterializer.MaterializeAsync(dbContext, game, utcNow, cancellationToken);
                }

                game.Status = GameStatus.Finished;
                game.FinishedAt = utcNow;
                game.HostGraceExpiresAt = null;
                game.Pin = null;
                game.StateVersion += 1;
                abandonedGames.Add(game);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            if (abandonedGames.Count == 0)
            {
                await transaction.CommitAsync(cancellationToken);
                if (recoverySucceeded)
                {
                    _failureTracker.ReportSuccess("AbandonedGameFinalizer");
                }
                return;
            }

            Guid[] gameIds = abandonedGames.Select(game => game.Id).ToArray();

            // Post-Game Expiry Window (RECON-WINDOW-001) - Enforces 24-hour read-only recovery boundary on participant session tokens via ExecuteUpdateAsync
            await dbContext.ParticipantSessionTokens
                .Where(token => gameIds.Contains(token.GameId) && token.ExpiresAt == null)
                .ExecuteUpdateAsync(setter => setter.SetProperty(token => token.ExpiresAt, utcNow.AddHours(24)), cancellationToken);

            // Batch Deterministic Ranking (SCORE-RANK-001) - Computes final rankings via PostgreSQL window function partitioned by game

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

            // Podium Materialization - Fetches top 3 players without tracking overhead for announcement payload
            List<Participant> podiumParticipants = await dbContext.Participants
                .AsNoTracking()
                .Where(participant => gameIds.Contains(participant.GameId) &&
                                      !participant.IsRemoved && participant.Rank <= 3)
                .OrderBy(participant => participant.GameId)
                .ThenBy(participant => participant.Rank)
                .ToListAsync(cancellationToken);

            Dictionary<Guid, List<PodiumParticipantDto>> podiumByGame = podiumParticipants
                .GroupBy(participant => participant.GameId)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(participant => new PodiumParticipantDto(
                        participant.Rank!.Value, participant.DisplayNickname, participant.TotalScore)).ToList());

            List<(Guid HostAccountId, Guid GameId, long StateVersion, object Payload)> notifications = new();
            foreach (Game game in abandonedGames)
            {
                List<PodiumParticipantDto> podium = podiumByGame.GetValueOrDefault(game.Id) ?? new();
                object gameEndedPayload = new
                {
                    gameId = game.Id,
                    status = "FINISHED",
                    stateVersion = game.StateVersion,
                    finishedAt = utcNow,
                    reason = "HostAbandoned",
                    podium
                };

                notifications.Add((game.HostAccountId, game.Id, game.StateVersion, gameEndedPayload));
            }

            // Atomic State Persistence - Commits all state changes before real-time event distribution
            await transaction.CommitAsync(cancellationToken);

            foreach ((Guid hostAccountId, Guid gameId, long stateVersion, object payload) in notifications)
            {
                _logger.LogWarning(
                    "Game finalized due to host abandonment. EventName={EventName} GameId={GameId} StateVersion={StateVersion}",
                    "GameAbandonmentFinalized", gameId, stateVersion);

                try
                {
                    // Post-Commit Broadcast Pattern (RT-ORD-001) - Publishes GameEnded event using CancellationToken.None to guarantee delivery
                    await notificationService.PublishGameEndedAsync(
                        hostAccountId, gameId, stateVersion, payload, CancellationToken.None);
                }
                catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
                {
                    _logger.LogError(exception,
                        "Failed to publish game abandonment event. GameId={GameId} StateVersion={StateVersion}",
                        gameId, stateVersion);
                }
            }
            if (recoverySucceeded)
            {
                _failureTracker.ReportSuccess("AbandonedGameFinalizer");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Critical Worker Failure Escalation (OPS-WORK-002) - Escalates error tracking if abandonment sweep fails repeatedly beyond 15 minutes
            _failureTracker.ReportFailure("AbandonedGameFinalizer", exception);
            _logger.LogError(
                exception,
                "Error occurred while sweeping abandoned games. EventName={EventName}",
                "GameAbandonmentSweepFailed");
        }
    }
}
