namespace Kahoot.Infrastructure.Realtime;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Player Presence Heartbeat Worker - Background daemon that periodically renews Redis presence leases for all active player sockets.
internal sealed class PlayerPresenceHeartbeatWorker : BackgroundService
{
    // Heartbeat Interval - 10-second tick interval ensuring sliding leases (45s) never lapse unexpectedly.
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    private readonly PlayerPresenceService _presence;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PlayerPresenceHeartbeatWorker> _logger;

    public PlayerPresenceHeartbeatWorker(
        PlayerPresenceService presence,
        TimeProvider timeProvider,
        ILogger<PlayerPresenceHeartbeatWorker> logger)
    {
        _presence = presence;
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
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
