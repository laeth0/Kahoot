using Kahoot.Application.Common.Exceptions;
using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Persistence;
using Kahoot.Application.Common.Results;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Kahoot.Application.Features.Auth.ChangePassword;

public sealed class ChangePasswordCommandHandler : ICommandHandler<ChangePasswordCommand>
{
    private readonly IAppDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ISocketEvictionService _socketEvictionService;
    private readonly TimeProvider _timeProvider;

    public ChangePasswordCommandHandler(
        IAppDbContext dbContext,
        ICurrentUser currentUser,
        IPasswordHasher passwordHasher,
        ISocketEvictionService socketEvictionService,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _passwordHasher = passwordHasher;
        _socketEvictionService = socketEvictionService;
        _timeProvider = timeProvider;
    }

    public async Task<Result> Handle(
        ChangePasswordCommand request,
        CancellationToken cancellationToken)
    {
        // Derived Identity (IDOR Prevention) - Target account is strictly resolved from ICurrentUser token, never from client input
        Guid? userId = _currentUser.UserId;
        if (!userId.HasValue || userId.Value == Guid.Empty)
        {
            return Result.Failure(AuthErrors.Unauthorized);
        }

        // Query Performance: AsNoTracking() and Select(PasswordHash) project only the needed column without entity overhead or database locks
        string? currentPasswordHash = await _dbContext.Users
            .AsNoTracking()
            .Where(candidate => candidate.Id == userId.Value && candidate.Status == UserStatus.Active)
            .Select(candidate => candidate.PasswordHash)
            .SingleOrDefaultAsync(cancellationToken);

        if (currentPasswordHash is null)
        {
            return Result.Failure(AuthErrors.Unauthorized);
        }

        // Off-Transaction Verification - Expensive Argon2id check executes outside DB transaction to avoid connection pool starvation
        try
        {
            bool isPasswordValid = await _passwordHasher.VerifyPasswordAsync(
                request.CurrentPassword,
                currentPasswordHash,
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

        // Off-Transaction Hashing - New hash is computed before opening the transaction to minimize database lock hold time
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

        DateTimeOffset now = _timeProvider.GetUtcNow();

        // Pessimistic Row Lock & Optimistic Guard - Re-checks password hash under row lock to prevent lost updates from concurrent changes
        await using IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Security & Concurrency: GetUserForUpdateAsync acquires a pessimistic row lock (SELECT FOR UPDATE) to guard against concurrent password mutations
        User? currentUser = await _dbContext.GetUserForUpdateAsync(userId.Value, cancellationToken);
        if (currentUser is null || currentUser.Status != UserStatus.Active ||
            !string.Equals(currentUser.PasswordHash, currentPasswordHash, StringComparison.Ordinal))
        {
            return Result.Failure(AuthErrors.InvalidCredentials);
        }

        // Complete Session Severance & Optimistic Guard: ExecuteUpdateAsync updates hash, increments TokenSecurityVersion, and checks PasswordHash matches in a single SQL UPDATE
        int updatedUsers = await _dbContext.Users
            .Where(candidate => candidate.Id == userId.Value
                             && candidate.Status == UserStatus.Active
                             && candidate.PasswordHash == currentPasswordHash)
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

        // Query Performance: ExecuteUpdateAsync revokes all active refresh tokens in a single SQL UPDATE
        await _dbContext.RefreshTokens
            .Where(token => token.UserId == userId.Value && token.RevokedAt == null)
            .ExecuteUpdateAsync(setter => setter.SetProperty(token => token.RevokedAt, now), cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        // Post-Commit Broadcast (AUTH-PASS-001): Evicts active SignalR sockets across cluster after DB transaction commits
        await _socketEvictionService.EvictUserSocketsAsync(userId.Value, cancellationToken);

        return Result.Success();
    }
}
