namespace Kahoot.Application.Features.Games;

using Kahoot.Application.Common.Persistence;
using Microsoft.EntityFrameworkCore;

public static class ParticipantRankMaterializer
{
    public static async Task MaterializeAsync(
        IAppDbContext dbContext,
        Guid gameId,
        Guid hostAccountId,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            WITH ranked AS (
                SELECT id, row_number() OVER (
                    ORDER BY total_score DESC, normalized_nickname COLLATE "C" ASC, id ASC
                ) AS final_rank
                FROM participants
                WHERE game_id = {gameId} AND host_account_id = {hostAccountId} AND NOT is_removed
            )
            UPDATE participants AS participant
            SET rank = ranked.final_rank::integer
            FROM ranked
            WHERE participant.id = ranked.id
            """, cancellationToken);

        await dbContext.Participants
            .Where(participant => participant.GameId == gameId &&
                                  participant.HostAccountId == hostAccountId && participant.IsRemoved)
            .ExecuteUpdateAsync(setter => setter.SetProperty(participant => participant.Rank, (int?)null), cancellationToken);
    }
}
