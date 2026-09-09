using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Errors;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Security;
using Kahoot.Application.Games.Common;
using Kahoot.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Application.Games.GetLeaderboard;

internal sealed class GetLeaderboardQueryHandler(IApplicationDbContext dbContext, ICurrentUser currentUser)
    : IQueryHandler<GetLeaderboardQuery, LeaderboardResponse>
{
    public async Task<Result<LeaderboardResponse>> Handle(
        GetLeaderboardQuery query,
        CancellationToken cancellationToken)
    {
        if (currentUser.HostId is not { } hostId)
        {
            return Result.Failure<LeaderboardResponse>(SharedErrors.Unauthorized);
        }

        bool ownsGame = await dbContext.GameSessions
            .AnyAsync(session => session.Id == query.GameId && session.HostId == hostId, cancellationToken);
        if (!ownsGame)
        {
            return Result.Failure<LeaderboardResponse>(GameErrors.NotFound);
        }

        return Result.Success(await LeaderboardBuilder.ReadAsync(dbContext, query.GameId, cancellationToken));
    }
}
