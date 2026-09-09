using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Messaging;
using Kahoot.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Application.Games.Presence;

internal sealed class AuthorizeHostGameQueryHandler(IApplicationDbContext dbContext)
    : IQueryHandler<AuthorizeHostGameQuery, bool>
{
    public async Task<Result<bool>> Handle(AuthorizeHostGameQuery query, CancellationToken cancellationToken)
    {
        bool ownsGame = await dbContext.GameSessions
            .AsNoTracking()
            .AnyAsync(
                session => session.Id == query.GameId && session.HostId == query.HostId,
                cancellationToken);

        return Result.Success(ownsGame);
    }
}
