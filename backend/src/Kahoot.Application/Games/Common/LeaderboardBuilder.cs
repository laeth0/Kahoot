using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Games.Leaderboard;
using Kahoot.Domain.Games;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Application.Games.Common;

internal static class LeaderboardBuilder
{
    public static LeaderboardResponse ApplyRanks(
        IReadOnlyList<Participant> trackedParticipants,
        ILeaderboardService leaderboardService)
    {
        IReadOnlyList<RankedParticipant> ranked = leaderboardService.Rank(
            trackedParticipants.Select(participant =>
                new ParticipantScore(participant.Id, participant.Nickname, participant.TotalScore)));

        Dictionary<Guid, int> rankByParticipant = ranked.ToDictionary(entry => entry.ParticipantId, entry => entry.Rank);
        foreach (Participant participant in trackedParticipants)
        {
            participant.LastRank = rankByParticipant[participant.Id];
        }

        return new LeaderboardResponse(
        [
            .. ranked.Select(entry => new LeaderboardEntryResponse(
                entry.Rank,
                entry.ParticipantId,
                entry.Nickname,
                entry.TotalScore))
        ]);
    }

    public static async Task<LeaderboardResponse> ReadAsync(
        IApplicationDbContext dbContext,
        Guid gameId,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.Participants
            .AsNoTracking()
            .Where(participant => participant.GameSessionId == gameId && !participant.IsRemoved)
            .OrderByDescending(participant => participant.TotalScore)
            .ThenBy(participant => participant.Nickname)
            .Select(participant => new
            {
                participant.Id,
                participant.Nickname,
                participant.TotalScore,
                participant.LastRank
            })
            .ToListAsync(cancellationToken);

        return new LeaderboardResponse(
        [
            .. rows.Select((row, index) => new LeaderboardEntryResponse(
                row.LastRank ?? index + 1,
                row.Id,
                row.Nickname,
                row.TotalScore))
        ]);
    }
}
