namespace Kahoot.Infrastructure.Realtime;

using Kahoot.Application.Common.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

internal sealed class SocketEvictionService : ISocketEvictionService
{
    private readonly ISubscriber _subscriber;
    private readonly RedisChannel _channel;
    private readonly ILogger<SocketEvictionService> _logger;

    public SocketEvictionService(
        IConnectionMultiplexer redis,
        IOptions<RealtimeOptions> options,
        ILogger<SocketEvictionService> logger)
    {
        _subscriber = redis.GetSubscriber();
        _channel = RedisChannel.Literal($"{options.Value.ChannelPrefix}:host-sockets:evict");
        _logger = logger;
    }

    public async Task EvictUserSocketsAsync(Guid hostAccountId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            await _subscriber.PublishAsync(_channel, hostAccountId.ToString("N"));
        }
        catch (Exception exception)
        {
            // The committed account status remains authoritative; the lease worker also evicts
            // suspended Hosts on its next database sweep.
            _logger.LogError(exception, "Host socket eviction notification failed. HostAccountId={HostAccountId}", hostAccountId);
        }
    }
}
