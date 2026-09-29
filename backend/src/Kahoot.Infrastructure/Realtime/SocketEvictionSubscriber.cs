namespace Kahoot.Infrastructure.Realtime;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

// Host Socket Eviction Subscriber - Listens for host invalidation messages and forcibly terminates local host and associated player sockets.
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

    // Pub/Sub Subscription Startup - Registers handler callback for cluster-wide host eviction events.
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
                // Cascade Socket Teardown - Forcibly terminates all host sockets and associated player sockets for suspended tenant.
                _presence.AbortHostConnections(hostAccountId);
                _playerPresence.AbortHostConnections(hostAccountId);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Host socket eviction failed. HostAccountId={HostAccountId}", hostAccountId);
            }
        });
    }

    // Pub/Sub Subscription Teardown - Unsubscribes from Redis eviction channel during host application shutdown.
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _subscriber.UnsubscribeAsync(_channel);
    }
}
