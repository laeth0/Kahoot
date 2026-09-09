using Kahoot.Application.Common.Interfaces;

namespace Kahoot.Application.Games.Leaderboard;

public sealed class LeaderboardService : ILeaderboardService, ISingletonService
{
    public IReadOnlyList<RankedParticipant> Rank(IEnumerable<ParticipantScore> participants) =>
        participants
            .OrderByDescending(participant => participant.TotalScore)
            .ThenBy(participant => participant.Nickname, StringComparer.OrdinalIgnoreCase)
            .Select((participant, index) => new RankedParticipant(
                participant.ParticipantId,
                participant.Nickname,
                participant.TotalScore,
                index + 1))
            .ToArray();
}
