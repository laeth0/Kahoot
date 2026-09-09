using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Errors;
using Kahoot.Application.Common.Security;
using Kahoot.Domain.Common;
using Kahoot.Domain.Games;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Application.Games.Common;

internal static class HostGameGuard
{
    public static async Task<Result<GameSession>> LoadOwnedGameAsync(
        IApplicationDbContext dbContext,
        ICurrentUser currentUser,
        Guid gameId,
        CancellationToken cancellationToken)
    {
        if (currentUser.HostId is not { } hostId)
        {
            return Result.Failure<GameSession>(SharedErrors.Unauthorized);
        }

        GameSession? game = await dbContext.GameSessions
            .FirstOrDefaultAsync(session => session.Id == gameId && session.HostId == hostId, cancellationToken);

        return game is null
            ? Result.Failure<GameSession>(GameErrors.NotFound)
            : Result.Success(game);
    }
}
