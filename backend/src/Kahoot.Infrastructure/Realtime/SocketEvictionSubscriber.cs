namespace Kahoot.Infrastructure.Realtime;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

internal sealed class SocketEvictionSubscriber : IHostedService
{
    private readonly ISubscriber _subscriber;
    private readonly HostPresenceService _presence;
    private readonly PlayerPresenceService _playerPresence;
    private readonly ILogger<SocketEvictionSubscriber> _logger;
    private readonly RedisChannel _channel;

    public SocketEvictionSubscriber(
        IConnectionMultiplexer redis,
        IOptions<RealtimeOptions> options,
        HostPresenceService presence,
        PlayerPresenceService playerPresence,
        ILogger<SocketEvictionSubscriber> logger)
    {
        _subscriber = redis.GetSubscriber();
        _presence = presence;
        _playerPresence = playerPresence;
        _logger = logger;
        _channel = RedisChannel.Literal($"{options.Value.ChannelPrefix}:host-sockets:evict");
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _subscriber.SubscribeAsync(_channel, (_, message) =>
        {
            if (!Guid.TryParse(message.ToString(), out Guid hostAccountId))
            {
                _logger.LogWarning("Invalid Host socket eviction message received.");
                return;
            }

            try
            {
                _presence.AbortHostConnections(hostAccountId);
                _playerPresence.AbortHostConnections(hostAccountId);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Host socket eviction failed. HostAccountId={HostAccountId}", hostAccountId);
            }
        });
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _subscriber.UnsubscribeAsync(_channel);
    }
}
