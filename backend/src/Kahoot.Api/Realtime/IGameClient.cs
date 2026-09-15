using Kahoot.Application.Games.Common;

namespace Kahoot.Api.Realtime;

public interface IGameClient
{
    Task ParticipantPresenceChanged(ParticipantPresenceResponse presence);

    Task ParticipantRemoved(Guid participantId);

    Task QuestionStarted(PlayerQuestionResponse question);

    Task QuestionStartedForHost(HostQuestionResponse question);

    Task QuestionEnded(QuestionResultsResponse results);

    Task LeaderboardUpdated(LeaderboardResponse leaderboard);

    Task GameEnded(LeaderboardResponse leaderboard);
}
