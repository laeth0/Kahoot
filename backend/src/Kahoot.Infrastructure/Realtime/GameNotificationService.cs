namespace Kahoot.Infrastructure.Realtime;

using Kahoot.Application.Common.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

// Realtime Game Notification Dispatcher - Dispatches partitioned real-time game events to isolated host and player SignalR channels.
public sealed class GameNotificationService : IGameNotificationService
{
    private readonly IHubContext<GameHub> _hubContext;
    private readonly ILogger<GameNotificationService> _logger;

    public GameNotificationService(
        IHubContext<GameHub> hubContext,
        ILogger<GameNotificationService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    // Dual-Audience Question Started Broadcast - Sends unredacted question metadata to host channel and choices/timer to player channel.
    public async Task PublishQuestionStartedAsync(
        Guid hostAccountId,
        Guid gameId,
        long stateVersion,
        object playerPayload,
        object hostPayload,
        CancellationToken cancellationToken = default)
    {
        // Tenant Channel Scoping - Derives isolated host and player channel identifiers scoped to hostAccountId and gameId.
        string hostGroup = $"host:{hostAccountId}:game:{gameId}:hosts";
        string playerGroup = $"host:{hostAccountId}:game:{gameId}:players";

        _logger.LogInformation(
            "Publishing QuestionStarted event. EventName={EventName} GameId={GameId} StateVersion={StateVersion}",
            "PublishQuestionStarted",
            gameId,
            stateVersion);

        // Asynchronous Channel Dispatch - Publishes player-facing payload and host-facing payload concurrently.
        await TrySendAsync(playerGroup, "QuestionStarted", playerPayload, gameId, cancellationToken);
        await TrySendAsync(hostGroup, "QuestionStartedForHost", hostPayload, gameId, cancellationToken);
    }

    // Question Ended Broadcast - Broadcasts question results, choice percentages, and correct answers to host and player groups.
    public async Task PublishQuestionEndedAsync(
        Guid hostAccountId,
        Guid gameId,
        long stateVersion,
        object payload,
        CancellationToken cancellationToken = default)
    {
        string hostGroup = $"host:{hostAccountId}:game:{gameId}:hosts";
        string playerGroup = $"host:{hostAccountId}:game:{gameId}:players";

        _logger.LogInformation(
            "Publishing QuestionEnded event. EventName={EventName} GameId={GameId} StateVersion={StateVersion}",
            "PublishQuestionEnded",
            gameId,
            stateVersion);

        await TrySendAsync(playerGroup, "QuestionEnded", payload, gameId, cancellationToken);
        await TrySendAsync(hostGroup, "QuestionEnded", payload, gameId, cancellationToken);
    }

    // Leaderboard Updated Broadcast - Emits ranked standings and score differentials to all game participants and the host.
    public async Task PublishLeaderboardUpdatedAsync(
        Guid hostAccountId,
        Guid gameId,
        long stateVersion,
        object payload,
        CancellationToken cancellationToken = default)
    {
        string hostGroup = $"host:{hostAccountId}:game:{gameId}:hosts";
        string playerGroup = $"host:{hostAccountId}:game:{gameId}:players";

        _logger.LogInformation(
            "Publishing LeaderboardUpdated event. EventName={EventName} GameId={GameId} StateVersion={StateVersion}",
            "PublishLeaderboardUpdated",
            gameId,
            stateVersion);

        await TrySendAsync(playerGroup, "LeaderboardUpdated", payload, gameId, cancellationToken);
        await TrySendAsync(hostGroup, "LeaderboardUpdated", payload, gameId, cancellationToken);
    }

    // Game Ended Broadcast - Dispatches final game termination frame and podium results to conclude the session.
    public async Task PublishGameEndedAsync(
        Guid hostAccountId,
        Guid gameId,
        long stateVersion,
        object payload,
        CancellationToken cancellationToken = default)
    {
        string hostGroup = $"host:{hostAccountId}:game:{gameId}:hosts";
        string playerGroup = $"host:{hostAccountId}:game:{gameId}:players";

        _logger.LogInformation(
            "Publishing GameEnded event. EventName={EventName} GameId={GameId} StateVersion={StateVersion}",
            "PublishGameEnded",
            gameId,
            stateVersion);

        await TrySendAsync(playerGroup, "GameEnded", payload, gameId, cancellationToken);
        await TrySendAsync(hostGroup, "GameEnded", payload, gameId, cancellationToken);
    }

    // Participant Presence Changed Broadcast - Notifies host and players of player connects, disconnects, and reconnects.
    public async Task PublishParticipantPresenceChangedAsync(
        Guid hostAccountId,
        Guid gameId,
        long presenceVersion,
        object payload,
        CancellationToken cancellationToken = default)
    {
        string hostGroup = $"host:{hostAccountId}:game:{gameId}:hosts";
        string playerGroup = $"host:{hostAccountId}:game:{gameId}:players";

        _logger.LogInformation(
            "Publishing ParticipantPresenceChanged event. EventName={EventName} GameId={GameId} PresenceVersion={PresenceVersion}",
            "PublishParticipantPresenceChanged",
            gameId,
            presenceVersion);

        await TrySendAsync(playerGroup, "ParticipantPresenceChanged", payload, gameId, cancellationToken);
        await TrySendAsync(hostGroup, "ParticipantPresenceChanged", payload, gameId, cancellationToken);
    }

    // Resilient Group Delivery - Traps transient transport exceptions without bubbling errors back into calling command handlers.
    private async Task TrySendAsync(
        string group,
        string eventName,
        object payload,
        Guid gameId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _hubContext.Clients.Group(group).SendAsync(eventName, payload, cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError(exception,
                "Game event publication failed after state commit. EventName={EventName} GameId={GameId}",
                eventName, gameId);
        }
    }
}
