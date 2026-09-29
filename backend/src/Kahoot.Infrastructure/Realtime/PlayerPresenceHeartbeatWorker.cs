namespace Kahoot.Infrastructure.Realtime;

using Kahoot.Domain.Enums;
using Kahoot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Player Presence Heartbeat Worker - Background daemon that periodically renews Redis presence leases for all active player sockets.
internal sealed class PlayerPresenceHeartbeatWorker : BackgroundService
{
    // Heartbeat Interval - 10-second tick interval ensuring sliding leases (45s) never lapse unexpectedly.
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    private readonly PlayerPresenceService _presence;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PlayerPresenceHeartbeatWorker> _logger;

    public PlayerPresenceHeartbeatWorker(
        PlayerPresenceService presence,
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        ILogger<PlayerPresenceHeartbeatWorker> logger)
    {
        _presence = presence;
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    // Periodic Player Lease Extension Loop - Iterates over local player connections and updates Redis lease expiration timestamps.
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new PeriodicTimer(Interval, _timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    // Redis Sorted Set Expiration Renewal - Extends sliding expiration on game and participant sorted sets.
                    await _presence.RenewAllAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Player presence lease renewal failed.");
                }

                try
                {
                    await AbortInvalidConnectionsAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Player socket validity sweep failed.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    // Redis Pub/Sub can lose an eviction during a disconnect; PostgreSQL remains authoritative.
    private async Task AbortInvalidConnectionsAsync(CancellationToken cancellationToken)
    {
        PlayerConnectionSnapshot[] connections = _presence.GetConnectionSnapshots();
        if (connections.Length == 0)
        {
            return;
        }

        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        const int batchSize = 500;
        for (int offset = 0; offset < connections.Length; offset += batchSize)
        {
            PlayerConnectionSnapshot[] batch = connections.Skip(offset).Take(batchSize).ToArray();
            Guid[] participantIds = batch.Select(connection => connection.ParticipantId).Distinct().ToArray();

            // Indexed participant lookup plus Host status fences missed removals and stale generations.
            Dictionary<Guid, long> validGenerations = await (
                from participant in dbContext.Participants.AsNoTracking()
                join host in dbContext.Users.AsNoTracking()
                    on participant.HostAccountId equals host.Id
                where participantIds.Contains(participant.Id) && !participant.IsRemoved &&
                      host.Status == UserStatus.Active && host.Role == UserRole.Host
                select new { participant.Id, participant.ConnectionGeneration })
                .ToDictionaryAsync(participant => participant.Id,
                    participant => participant.ConnectionGeneration, cancellationToken);

            foreach (PlayerConnectionSnapshot connection in batch)
            {
                if (!validGenerations.TryGetValue(connection.ParticipantId, out long generation) ||
                    connection.ConnectionGeneration != generation)
                {
                    _presence.AbortConnection(connection.ConnectionId);
                }
            }
        }
    }
}
