using System.Diagnostics;
using Kahoot.Application.Common.Observability;
using Kahoot.Application.Games.Common;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Kahoot.Api.Realtime;

public sealed class GameNotifier(
    IHubContext<GameHub, IGameClient> hub,
    IKahootTelemetry telemetry,
    ILogger<GameNotifier> logger)
{
    public Task ParticipantJoinedAsync(Guid gameId, GameParticipantResponse participant) =>
        BroadcastAsync("ParticipantJoined", () => Everyone(gameId).ParticipantJoined(participant));

    public Task ParticipantLeftAsync(Guid gameId, Guid participantId) =>
        BroadcastAsync("ParticipantLeft", () => Everyone(gameId).ParticipantLeft(participantId));

    public async Task ParticipantRemovedAsync(Guid gameId, Guid participantId, string? connectionId = null)
    {
        if (!string.IsNullOrEmpty(connectionId))
        {
            try
            {
                await hub.Groups.RemoveFromGroupAsync(connectionId, GameGroups.Players(gameId));
                await hub.Clients.Client(connectionId).ParticipantRemoved(participantId);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Failed to evict kicked connection {ConnectionId}", connectionId);
            }
        }

        await BroadcastAsync("ParticipantRemoved", () => Everyone(gameId).ParticipantRemoved(participantId));
    }

    public Task QuestionStartedAsync(Guid gameId, QuestionStartedResponse question) =>
        BroadcastAsync("QuestionStarted", () =>
        {
            Task players = hub.Clients.Group(GameGroups.Players(gameId)).QuestionStarted(question.Player);
            Task host = hub.Clients.Group(GameGroups.Host(gameId)).QuestionStartedForHost(question.Host);
            return Task.WhenAll(players, host);
        });

    public Task QuestionEndedAsync(Guid gameId, QuestionResultsResponse results) =>
        BroadcastAsync("QuestionEnded", () => Everyone(gameId).QuestionEnded(results));

    public Task LeaderboardUpdatedAsync(Guid gameId, LeaderboardResponse leaderboard) =>
        BroadcastAsync("LeaderboardUpdated", () => Everyone(gameId).LeaderboardUpdated(leaderboard));

    public Task GameEndedAsync(Guid gameId, LeaderboardResponse leaderboard) =>
        BroadcastAsync("GameEnded", () => Everyone(gameId).GameEnded(leaderboard));

    private async Task BroadcastAsync(string eventName, Func<Task> broadcastAction)
    {
        long startTimestamp = Stopwatch.GetTimestamp();
        string outcome = "failure";

        try
        {
            await broadcastAction();
            outcome = "success";
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to broadcast real-time event {EventName}", eventName);
        }
        finally
        {
            double durationSeconds = Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds;
            telemetry.RecordBroadcast(eventName, outcome, durationSeconds);
        }
    }

    private IGameClient Everyone(Guid gameId) =>
        hub.Clients.Groups(GameGroups.Players(gameId), GameGroups.Host(gameId));
}
