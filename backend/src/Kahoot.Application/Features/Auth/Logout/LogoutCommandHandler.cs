using System.Security.Cryptography;
using System.Text;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Persistence;
using Kahoot.Application.Common.Results;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Application.Features.Auth.Logout;

public sealed class LogoutCommandHandler : ICommandHandler<LogoutCommand>
{
    private readonly IAppDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public LogoutCommandHandler(
        IAppDbContext dbContext,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<Result> Handle(
        LogoutCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RawRefreshToken))
        {
            return Result.Success();
        }

        // 1. Hash the presented raw refresh token using SHA-256 (raw token is never queried or stored)
        byte[] presentedTokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(request.RawRefreshToken));

        // 2. Indexed lookup by TokenHash
        var token = await _dbContext.RefreshTokens
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.TokenHash == presentedTokenHash, cancellationToken);

        if (token is null)
        {
            return Result.Success();
        }

        var now = _timeProvider.GetUtcNow();

        // 3. Atomically revoke every non-revoked refresh token belonging to that family
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var user = await _dbContext.GetUserForUpdateAsync(token.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Success();
        }

        await _dbContext.RefreshTokens
            .Where(candidate => candidate.UserId == token.UserId &&
                                candidate.TokenFamilyId == token.TokenFamilyId &&
                                candidate.RevokedAt == null)
            .ExecuteUpdateAsync(setter => setter.SetProperty(t => t.RevokedAt, now), cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
