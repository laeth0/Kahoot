using Kahoot.Application.Common.Exceptions;
using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Persistence;
using Kahoot.Application.Common.Results;
using Kahoot.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Application.Features.Auth.ChangePassword;

public sealed class ChangePasswordCommandHandler : ICommandHandler<ChangePasswordCommand>
{
    private readonly IAppDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IPasswordHasher _passwordHasher;
    private readonly TimeProvider _timeProvider;

    public ChangePasswordCommandHandler(
        IAppDbContext dbContext,
        ICurrentUser currentUser,
        IPasswordHasher passwordHasher,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _passwordHasher = passwordHasher;
        _timeProvider = timeProvider;
    }

    public async Task<Result> Handle(
        ChangePasswordCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Resolve account exclusively from the authenticated user
        var userId = _currentUser.UserId;
        if (!userId.HasValue || userId.Value == Guid.Empty)
        {
            return Result.Failure(AuthErrors.Unauthorized);
        }

        // 2. Fetch the current active user state without transaction/locks
        var user = await _dbContext.Users
            .AsNoTracking()
            .Where(candidate => candidate.Id == userId.Value && candidate.Status == UserStatus.Active)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.PasswordHash
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (user is null)
        {
            return Result.Failure(AuthErrors.Unauthorized);
        }

        // 3. Verify current password under AUTH-HASH-002 gate without holding DB locks
        try
        {
            bool isPasswordValid = await _passwordHasher.VerifyPasswordAsync(
                request.CurrentPassword,
                user.PasswordHash,
                cancellationToken);

            if (!isPasswordValid)
            {
                return Result.Failure(AuthErrors.InvalidCredentials);
            }
        }
        catch (PasswordHashingRateLimitedException)
        {
            return Result.Failure(AuthErrors.RateLimited);
        }

        // 4. Hash new password under AUTH-HASH-002 gate outside any database transaction
        string newPasswordHash;
        try
        {
            newPasswordHash = await _passwordHasher.HashPasswordAsync(
                request.NewPassword,
                cancellationToken);
        }
        catch (PasswordHashingRateLimitedException)
        {
            return Result.Failure(AuthErrors.RateLimited);
        }

        var now = _timeProvider.GetUtcNow();

        // 5. Atomically commit password update, security version increment, and all refresh-token revocation
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var currentUser = await _dbContext.GetUserForUpdateAsync(userId.Value, cancellationToken);
        if (currentUser is null || currentUser.Status != UserStatus.Active ||
            !string.Equals(currentUser.PasswordHash, user.PasswordHash, StringComparison.Ordinal))
        {
            return Result.Failure(AuthErrors.InvalidCredentials);
        }

        // Optimistic concurrency guarantee: ensure the password hash has not changed concurrently
        int updatedUsers = await _dbContext.Users
            .Where(candidate => candidate.Id == userId.Value
                             && candidate.Status == UserStatus.Active
                             && candidate.PasswordHash == user.PasswordHash)
            .ExecuteUpdateAsync(setter => setter
                .SetProperty(u => u.PasswordHash, newPasswordHash)
                .SetProperty(u => u.TokenSecurityVersion, u => u.TokenSecurityVersion + 1)
                .SetProperty(u => u.Revision, u => u.Revision + 1)
                .SetProperty(u => u.UpdatedAt, now), cancellationToken);

        if (updatedUsers == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(AuthErrors.InvalidCredentials);
        }

        await _dbContext.RefreshTokens
            .Where(token => token.UserId == userId.Value && token.RevokedAt == null)
            .ExecuteUpdateAsync(setter => setter.SetProperty(token => token.RevokedAt, now), cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
