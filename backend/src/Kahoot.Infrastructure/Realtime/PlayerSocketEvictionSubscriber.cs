namespace Kahoot.Infrastructure.Realtime;

using Kahoot.Application.Features.Games.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

// Player Socket Eviction Subscriber - Background listener consuming Redis pub/sub eviction frames to abort stale or kicked player sockets across nodes.
internal sealed class PlayerSocketEvictionSubscriber : BackgroundService
{
    private readonly ISubscriber _subscriber;
    private readonly RedisChannel _channel;
    private readonly PlayerPresenceService _presence;
    private readonly IHubContext<GameHub> _hubContext;
    private readonly ILogger<PlayerSocketEvictionSubscriber> _logger;
    private readonly TaskCompletionSource _subscriptionReady = new(TaskCreationOptions.RunContinuationsAsynchronously);

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

    // Do not advertise readiness until this replica is listening for removal and fencing frames.
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await base.StartAsync(cancellationToken);
        await _subscriptionReady.Task.WaitAsync(cancellationToken);
    }

    // Message Consumption Loop - Reads pub/sub eviction messages and executes fencing or kick procedures.
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ChannelMessageQueue queue;
        try
        {
            queue = await _subscriber.SubscribeAsync(_channel);
            _subscriptionReady.TrySetResult();
        }
        catch (Exception exception)
        {
            _subscriptionReady.TrySetException(exception);
            throw;
        }

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

                // Distributed Connection Generation Fencing - Aborts older sockets presenting generation strictly below published threshold.
                if (identifiers[0] == "fence" && identifiers.Length == 4 &&
                    long.TryParse(identifiers[3], out long generation))
                {
                    _presence.AbortStaleParticipantConnections(participantId, generation);
                    continue;
                }

                if (identifiers[0] != "kick" || identifiers.Length != 4 ||
                    !long.TryParse(identifiers[3], out long stateVersion))
                {
                    _logger.LogWarning("Invalid player socket eviction message received.");
                    continue;
                }

                // A single bounded send avoids serial waits across a participant's duplicate sockets.
                string[] connectionIds = _presence.GetParticipantConnectionIds(participantId);
                if (connectionIds.Length > 0)
                {
                    using CancellationTokenSource sendTimeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    sendTimeout.CancelAfter(TimeSpan.FromMilliseconds(50));
                    try
                    {
                        await _hubContext.Clients.Clients(connectionIds).SendAsync(
                            "ParticipantRemoved",
                            new ParticipantRemovedEvent(gameId, stateVersion, participantId),
                            sendTimeout.Token);
                    }
                    catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                    {
                        _logger.LogWarning(exception,
                            "Player removal frame could not be sent before socket eviction. ParticipantId={ParticipantId}",
                            participantId);
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
