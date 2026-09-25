using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Persistence;
using Kahoot.Application.Common.Results;
using Kahoot.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Application.Features.Auth.LogoutAll;

public sealed class LogoutAllCommandHandler : ICommandHandler<LogoutAllCommand>
{
    private readonly IAppDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;

    public LogoutAllCommandHandler(
        IAppDbContext dbContext,
        ICurrentUser currentUser,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<Result> Handle(
        LogoutAllCommand request,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (!userId.HasValue || userId.Value == Guid.Empty)
        {
            return Result.Failure(AuthErrors.Unauthorized);
        }

        var now = _timeProvider.GetUtcNow();

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // 1. Atomically revoke every active refresh token belonging to this user across all families/sessions
        await _dbContext.RefreshTokens
            .Where(token => token.UserId == userId.Value && token.RevokedAt == null)
            .ExecuteUpdateAsync(setter => setter.SetProperty(t => t.RevokedAt, now), cancellationToken);

        // 2. Increment TokenSecurityVersion to invalidate all outstanding JWTs
        int updatedUsers = await _dbContext.Users
            .Where(user => user.Id == userId.Value && user.Status == UserStatus.Active)
            .ExecuteUpdateAsync(setter => setter
                .SetProperty(u => u.TokenSecurityVersion, u => u.TokenSecurityVersion + 1)
                .SetProperty(u => u.UpdatedAt, now), cancellationToken);

        if (updatedUsers == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(AuthErrors.Unauthorized);
        }

        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
