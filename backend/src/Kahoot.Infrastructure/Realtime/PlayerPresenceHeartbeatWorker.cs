namespace Kahoot.Infrastructure.Realtime;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

internal sealed class PlayerPresenceHeartbeatWorker : BackgroundService
{
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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new PeriodicTimer(Interval, _timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
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
