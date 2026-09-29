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
        // System Design & Query Performance: SQL CTE with row_number() window function materializes dense rankings directly in the database without loading players into memory
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

        // Query Performance: ExecuteUpdateAsync clears rank for removed participants in a single atomic statement without entity materialization
        await dbContext.Participants
            .Where(participant => participant.GameId == gameId &&
                                  participant.HostAccountId == hostAccountId && participant.IsRemoved)
            .ExecuteUpdateAsync(setter => setter.SetProperty(participant => participant.Rank, (int?)null), cancellationToken);
    }
}
