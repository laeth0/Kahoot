using Kahoot.Application.Common.Interfaces;
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
    private const int BatchSize = 10;
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan BatchYieldInterval = TimeSpan.FromMilliseconds(50);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISuspensionFinalizerChannel _channel;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SuspensionFinalizerWorker> _logger;

    public SuspensionFinalizerWorker(
        IServiceScopeFactory scopeFactory,
        ISuspensionFinalizerChannel channel,
        TimeProvider timeProvider,
        ILogger<SuspensionFinalizerWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _channel = channel;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Suspension finalizer worker starting. Executing startup sweep. EventName={EventName}", "SuspensionFinalizerStarting");

        // Step: On boot, immediately scan and resume any incomplete finalizations after crash or restart (ACCT-RISK-002, ACCT-TEST-008)
        try
        {
            await ProcessAllPendingSuspensionsAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error during initial suspension finalization sweep. EventName={EventName}", "SuspensionFinalizerStartupError");
        }

        using PeriodicTimer timer = new PeriodicTimer(SweepInterval, _timeProvider);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                Task<bool> channelTask = _channel.WaitToReadAsync(stoppingToken).AsTask();
                Task<bool> timerTask = timer.WaitForNextTickAsync(stoppingToken).AsTask();

                Task<bool> completedTask = await Task.WhenAny(channelTask, timerTask);

                if (completedTask == channelTask && await channelTask)
                {
                    while (_channel.TryRead(out Guid hostAccountId))
                    {
                        await FinalizeHostGamesAsync(hostAccountId, stoppingToken);
                    }
                }

                if (completedTask == timerTask && await timerTask)
                {
                    await ProcessAllPendingSuspensionsAsync(stoppingToken);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Application shutdown cancels the worker loop cleanly
        }
    }

    private async Task ProcessAllPendingSuspensionsAsync(CancellationToken cancellationToken)
    {
        List<Guid> pendingHostIds;
        await using (AsyncServiceScope scope = _scopeFactory.CreateAsyncScope())
        {
            AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            pendingHostIds = await dbContext.Users
                .AsNoTracking()
                .Where(u => u.Status == UserStatus.Suspended && u.TerminationPending)
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);
        }

        foreach (Guid hostId in pendingHostIds)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            await FinalizeHostGamesAsync(hostId, cancellationToken);
        }
    }

    private async Task FinalizeHostGamesAsync(Guid hostAccountId, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            bool hasMoreGames;
            await using (AsyncServiceScope scope = _scopeFactory.CreateAsyncScope())
            {
                AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                await using IDbContextTransaction transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

                // Step: Acquire row lock on host user row to serialize finalization across cluster replicas
                User? host = await dbContext.GetUserForUpdateAsync(hostAccountId, cancellationToken);
                if (host is null || !host.TerminationPending)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    break;
                }

                DateTimeOffset cutoffTimestamp = host.UpdatedAt;

                // Step: Fetch bounded batch of unfinished games (<= 10 games per transaction) (ACCT-SUSP-004, ACCT-RISK-001)
                List<Game> batchGames = await dbContext.Games
                    .Where(g => g.HostAccountId == hostAccountId && g.Status != GameStatus.Finished)
                    .OrderBy(g => g.CreatedAt)
                    .Take(BatchSize)
                    .ToListAsync(cancellationToken);

                if (batchGames.Count == 0)
                {
                    // State: All games already materialized; clear TerminationPending flag
                    await dbContext.Users
                        .Where(u => u.Id == hostAccountId && u.TerminationPending)
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(u => u.TerminationPending, false)
                            .SetProperty(u => u.UpdatedAt, _timeProvider.GetUtcNow()), cancellationToken);

                    await transaction.CommitAsync(cancellationToken);
                    _logger.LogInformation("Completed suspension game finalization for host {HostAccountId}. EventName={EventName}", hostAccountId, "SuspensionFinalizationCompleted");
                    break;
                }

                List<Guid> gameIds = batchGames.Select(g => g.Id).ToList();

                List<Participant> participants = await dbContext.Participants
                    .Where(p => gameIds.Contains(p.GameId))
                    .ToListAsync(cancellationToken);

                ILookup<Guid, Participant> participantsByGame = participants.ToLookup(p => p.GameId);

                // Step: Materialize final ranks, set FINISHED status, set FinishedAt, and release PINs
                foreach (Game game in batchGames)
                {
                    game.Status = GameStatus.Finished;
                    game.FinishedAt = cutoffTimestamp;
                    game.Pin = null;
                    game.IsTerminatedBySuspension = true;

                    List<Participant> gameParticipants = participantsByGame[game.Id]
                        .OrderByDescending(p => p.TotalScore)
                        .ThenBy(p => p.CreatedAt)
                        .ToList();

                    for (int i = 0; i < gameParticipants.Count; i++)
                    {
                        if (i > 0 && gameParticipants[i].TotalScore == gameParticipants[i - 1].TotalScore)
                        {
                            gameParticipants[i].Rank = gameParticipants[i - 1].Rank;
                        }
                        else
                        {
                            gameParticipants[i].Rank = i + 1;
                        }
                    }
                }

                await dbContext.SaveChangesAsync(cancellationToken);

                hasMoreGames = await dbContext.Games
                    .AnyAsync(g => g.HostAccountId == hostAccountId && g.Status != GameStatus.Finished, cancellationToken);

                if (!hasMoreGames)
                {
                    await dbContext.Users
                        .Where(u => u.Id == hostAccountId && u.TerminationPending)
                        .ExecuteUpdateAsync(s => s
                            .SetProperty(u => u.TerminationPending, false)
                            .SetProperty(u => u.UpdatedAt, _timeProvider.GetUtcNow()), cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);

                if (!hasMoreGames)
                {
                    _logger.LogInformation("Completed suspension game finalization for host {HostAccountId}. EventName={EventName}", hostAccountId, "SuspensionFinalizationCompleted");
                    break;
                }
            }

            // Step: Yield briefly between batches to prevent DB connection starvation
            await Task.Delay(BatchYieldInterval, _timeProvider, cancellationToken);
        }
    }
}
