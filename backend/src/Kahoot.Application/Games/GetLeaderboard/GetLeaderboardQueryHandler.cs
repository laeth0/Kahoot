using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Security;
using Kahoot.Application.Games.Common;
using Kahoot.Domain.Common;

namespace Kahoot.Application.Games.GetLeaderboard;

internal sealed class GetLeaderboardQueryHandler(IApplicationDbContext dbContext, ICurrentUser currentUser)
    : IQueryHandler<GetLeaderboardQuery, LeaderboardResponse>
{
    public async Task<Result<LeaderboardResponse>> Handle(
        GetLeaderboardQuery query,
        CancellationToken cancellationToken)
    {
        Result ownership = await HostGameGuard.EnsureOwnedAsync(
            dbContext, currentUser, query.GameId, cancellationToken);
        if (ownership.IsFailure)
        {
            return Result.Failure<LeaderboardResponse>(ownership.Error);
        }

        return Result.Success(await LeaderboardBuilder.ReadAsync(dbContext, query.GameId, cancellationToken));
    }
}
