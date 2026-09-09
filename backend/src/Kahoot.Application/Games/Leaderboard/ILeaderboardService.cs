namespace Kahoot.Application.Games.Leaderboard;

public interface ILeaderboardService
{
    IReadOnlyList<RankedParticipant> Rank(IEnumerable<ParticipantScore> participants);
}

public sealed record ParticipantScore(Guid ParticipantId, string Nickname, int TotalScore);

public sealed record RankedParticipant(Guid ParticipantId, string Nickname, int TotalScore, int Rank);
