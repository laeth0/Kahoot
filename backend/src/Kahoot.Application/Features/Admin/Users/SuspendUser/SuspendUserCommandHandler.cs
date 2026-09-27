using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Persistence;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Admin;
using Kahoot.Application.Features.Auth;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Kahoot.Application.Features.Admin.Users.SuspendUser;

public sealed class SuspendUserCommandHandler : ICommandHandler<SuspendUserCommand, SuspendUserResult>
{
    private readonly IAppDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;
    private readonly ISocketEvictionService _socketEvictionService;
    private readonly ISuspensionFinalizerChannel _suspensionFinalizerChannel;

    public SuspendUserCommandHandler(
        IAppDbContext dbContext,
        ICurrentUser currentUser,
        TimeProvider timeProvider,
        ISocketEvictionService socketEvictionService,
        ISuspensionFinalizerChannel suspensionFinalizerChannel)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
        _socketEvictionService = socketEvictionService;
        _suspensionFinalizerChannel = suspensionFinalizerChannel;
    }

    public async Task<Result<SuspendUserResult>> Handle(
        SuspendUserCommand request,
        CancellationToken cancellationToken)
    {
        // State: Caller authorization check (must be an authenticated SystemAdmin)
        if (!_currentUser.IsAuthenticated || !string.Equals(_currentUser.Role, nameof(UserRole.SystemAdmin), StringComparison.Ordinal))
        {
            return Result.Failure<SuspendUserResult>(AuthErrors.Forbidden);
        }

        Guid? adminId = _currentUser.UserId;

        // Step: Acquire pessimistic row lock (SELECT FOR UPDATE) inside transaction to serialize mutation
        await using IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        User? user = await _dbContext.GetUserForUpdateAsync(request.AccountId, cancellationToken);

        // State: Target account validation (must exist and belong to Host role)
        if (user is null || user.Role != UserRole.Host)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure<SuspendUserResult>(AccountErrors.NotFound);
        }

        // State: Outcome B - Idempotent replay when previous commit succeeded but client dropped response
        if (user.Status == UserStatus.Suspended && user.Revision == request.Revision + 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Success(new SuspendUserResult(user.TerminationPending));
        }

        // State: Optimistic Concurrency Control (OCC) - Detect concurrent modification or stale revision
        if (user.Revision != request.Revision)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure<SuspendUserResult>(AccountErrors.ConcurrentModification);
        }

        // State: Idempotent no-op - Account is already suspended under the requested revision
        if (user.Status == UserStatus.Suspended)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Success(new SuspendUserResult(user.TerminationPending));
        }

        // Step: Detect any active unfinished games owned by host (CREATED, LOBBY, QUESTION_ACTIVE, etc.)
        bool hasUnfinishedGames = await _dbContext.Games
            .AnyAsync(game => game.HostAccountId == request.AccountId && game.Status != GameStatus.Finished, cancellationToken);

        // Game commands must reject a suspended host until the worker materializes each game.

        DateTimeOffset now = _timeProvider.GetUtcNow();

        // Step: Revoke all active refresh tokens for the target host account
        await _dbContext.RefreshTokens
            .Where(token => token.UserId == request.AccountId && token.RevokedAt == null)
            .ExecuteUpdateAsync(setter => setter.SetProperty(t => t.RevokedAt, now), cancellationToken);

        // Step: Atomic user update - Suspend status, increment TokenSecurityVersion (JWT cutoff), advance revision
        int updatedCount = await _dbContext.Users
            .Where(u => u.Id == request.AccountId && u.Revision == request.Revision)
            .ExecuteUpdateAsync(setter => setter
                .SetProperty(u => u.Status, UserStatus.Suspended)
                .SetProperty(u => u.TokenSecurityVersion, u => u.TokenSecurityVersion + 1)
                .SetProperty(u => u.Revision, u => u.Revision + 1)
                .SetProperty(u => u.TerminationPending, hasUnfinishedGames)
                .SetProperty(u => u.UpdatedAt, now)
                .SetProperty(u => u.UpdatedBy, adminId), cancellationToken);

        // State: Concurrency check - Verify user row was updated without intervening changes
        if (updatedCount == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure<SuspendUserResult>(AccountErrors.ConcurrentModification);
        }

        // Step: Atomically commit Phase 1 suspension transaction
        await transaction.CommitAsync(cancellationToken);

        // Step: Trigger Phase 2 background worker if host has active unfinished games (ACCT-SUSP-004)
        if (hasUnfinishedGames)
        {
            _suspensionFinalizerChannel.NotifySuspension(request.AccountId);
        }

        // Step: Evict active SignalR socket connections for host and players across cluster (ACCT-SLO-002)
        await _socketEvictionService.EvictUserSocketsAsync(request.AccountId, cancellationToken);

        return Result.Success(new SuspendUserResult(hasUnfinishedGames));
    }
}
