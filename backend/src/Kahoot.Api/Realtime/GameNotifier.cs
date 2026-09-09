using Kahoot.Application.Games.Common;
using Microsoft.AspNetCore.SignalR;

namespace Kahoot.Api.Realtime;

public sealed class GameNotifier(IHubContext<GameHub, IGameClient> hub)
{
    public Task ParticipantJoinedAsync(Guid gameId, GameParticipantResponse participant) =>
        Everyone(gameId).ParticipantJoined(participant);

    public Task ParticipantLeftAsync(Guid gameId, Guid participantId) =>
        hub.Clients.Group(GameGroups.Host(gameId)).ParticipantLeft(participantId);

    public Task ParticipantRemovedAsync(Guid gameId, Guid participantId) =>
        Everyone(gameId).ParticipantRemoved(participantId);

    public Task QuestionStartedAsync(Guid gameId, QuestionStartedResponse question)
    {
        Task players = hub.Clients.Group(GameGroups.Players(gameId)).QuestionStarted(question.Player);
        Task host = hub.Clients.Group(GameGroups.Host(gameId)).QuestionStartedForHost(question.Host);
        return Task.WhenAll(players, host);
    }

    public Task QuestionEndedAsync(Guid gameId, QuestionResultsResponse results) =>
        Everyone(gameId).QuestionEnded(results);

    public Task LeaderboardUpdatedAsync(Guid gameId, LeaderboardResponse leaderboard) =>
        Everyone(gameId).LeaderboardUpdated(leaderboard);

    public Task GameEndedAsync(Guid gameId, LeaderboardResponse leaderboard) =>
        Everyone(gameId).GameEnded(leaderboard);

    private IGameClient Everyone(Guid gameId) =>
        hub.Clients.Groups(GameGroups.Players(gameId), GameGroups.Host(gameId));
}
