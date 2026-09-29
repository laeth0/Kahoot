namespace Kahoot.Infrastructure.Realtime;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

internal sealed class PlayerSocketEvictionSubscriber : BackgroundService
{
    private readonly ISubscriber _subscriber;
    private readonly RedisChannel _channel;
    private readonly PlayerPresenceService _presence;
    private readonly IHubContext<GameHub> _hubContext;
    private readonly ILogger<PlayerSocketEvictionSubscriber> _logger;

    public PlayerSocketEvictionSubscriber(
        IConnectionMultiplexer redis,
        IOptions<RealtimeOptions> options,
        PlayerPresenceService presence,
        IHubContext<GameHub> hubContext,
        ILogger<PlayerSocketEvictionSubscriber> logger)
    {
        _subscriber = redis.GetSubscriber();
        _channel = RedisChannel.Literal($"{options.Value.ChannelPrefix}:player-sockets:evict");
        _presence = presence;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ChannelMessageQueue queue = await _subscriber.SubscribeAsync(_channel);
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ChannelMessage message = await queue.ReadAsync(stoppingToken);
                string[] identifiers = message.Message.ToString().Split(':');
                if (identifiers.Length < 3 ||
                    !Guid.TryParseExact(identifiers[1], "N", out Guid gameId) ||
                    !Guid.TryParseExact(identifiers[2], "N", out Guid participantId))
                {
                    _logger.LogWarning("Invalid player socket eviction message received.");
                    continue;
                }

                if (identifiers[0] == "fence" && identifiers.Length == 4 &&
                    long.TryParse(identifiers[3], out long generation))
                {
                    _presence.AbortStaleParticipantConnections(participantId, generation);
                    continue;
                }

                if (identifiers[0] != "kick" || identifiers.Length != 3)
                {
                    _logger.LogWarning("Invalid player socket eviction message received.");
                    continue;
                }

                string[] connectionIds = _presence.GetParticipantConnectionIds(participantId);
                foreach (string connectionId in connectionIds)
                {
                    try
                    {
                        await _hubContext.Clients.Client(connectionId).SendAsync(
                            "ParticipantRemoved",
                            new { gameId, participantId, reason = "KickedByHost" },
                            stoppingToken);
                    }
                    catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                    {
                        _logger.LogError(exception,
                            "Player removal frame failed. ParticipantId={ParticipantId}", participantId);
                    }
                }

                _presence.AbortParticipantConnections(participantId);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            await queue.UnsubscribeAsync();
        }
    }
}
