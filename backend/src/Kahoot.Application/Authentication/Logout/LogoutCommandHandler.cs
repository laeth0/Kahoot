using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Messaging;
using Kahoot.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Application.Authentication.Logout;

internal sealed class LogoutCommandHandler(
    IApplicationDbContext dbContext,
    ITokenHasher tokenHasher,
    TimeProvider timeProvider) : ICommandHandler<LogoutCommand>
{
    public async Task<Result> Handle(LogoutCommand command, CancellationToken cancellationToken)
    {
        string tokenHash = tokenHasher.Hash(command.RefreshToken);

        var token = await dbContext.RefreshTokens
            .AsNoTracking()
            .Where(t => t.TokenHash == tokenHash)
            .Select(t => new { t.FamilyId })
            .FirstOrDefaultAsync(cancellationToken);

        if (token is not null)
        {
            await dbContext.RefreshTokens
                .Where(t => t.FamilyId == token.FamilyId && t.RevokedAt == null)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(t => t.RevokedAt, timeProvider.GetUtcNow().UtcDateTime),
                    cancellationToken);
        }

        return Result.Success();
    }
}
