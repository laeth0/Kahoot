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
        // Administrative Role Authorization - Restricts host suspension strictly to authenticated SystemAdmin callers (ACCT-SEC-001)
        if (!_currentUser.IsAuthenticated || !string.Equals(_currentUser.Role, nameof(UserRole.SystemAdmin), StringComparison.Ordinal))
        {
            return Result.Failure<SuspendUserResult>(AuthErrors.Forbidden);
        }

        Guid? adminId = _currentUser.UserId;

        // Transactional Consistency Boundary - Serializes Phase 1 suspension cutoff in a single lightweight transaction
        await using IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Pessimistic Row Lock (SELECT FOR UPDATE) - Locks target host account row to serialize concurrent lifecycle changes
        User? user = await _dbContext.GetUserForUpdateAsync(request.AccountId, cancellationToken);

        // Role & Entity Invariant Check - Verifies account exists and belongs strictly to the Host role (ACCT-ERR-004)
        if (user is null || user.Role != UserRole.Host)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure<SuspendUserResult>(AccountErrors.NotFound);
        }

        // Idempotent Replay (Outcome B) - Returns success if previous commit succeeded but network response was dropped
        if (user.Status == UserStatus.Suspended && user.Revision == request.Revision + 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Success(new SuspendUserResult(user.TerminationPending));
        }

        // Optimistic Concurrency Control (OCC) - Detects state drift or concurrent modifications via revision mismatch (ACCT-BOUND-001, ACCT-ERR-005)
        if (user.Revision != request.Revision)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure<SuspendUserResult>(AccountErrors.ConcurrentModification);
        }

        // Idempotent No-Op - Fast path if account is already suspended under current revision
        if (user.Status == UserStatus.Suspended)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Success(new SuspendUserResult(user.TerminationPending));
        }

        // Active Game Detection (ACCT-SUSP-002) - Detects unfinished games to determine whether Phase 2 background finalization is required
        bool hasUnfinishedGames = await _dbContext.Games
            .AnyAsync(game => game.HostAccountId == request.AccountId && game.Status != GameStatus.Finished, cancellationToken);

        DateTimeOffset now = _timeProvider.GetUtcNow();

        // In-Database Bulk Token Revocation - Immediately revokes all active refresh tokens without memory loading
        await _dbContext.RefreshTokens
            .Where(token => token.UserId == request.AccountId && token.RevokedAt == null)
            .ExecuteUpdateAsync(setter => setter.SetProperty(t => t.RevokedAt, now), cancellationToken);

        // Immediate Suspension Cutoff - Atomically transitions to Suspended, flags terminationPending, and increments TokenSecurityVersion for instant cluster-wide JWT invalidation (ACCT-SLO-001)
        int updatedCount = await _dbContext.Users
            .Where(u => u.Id == request.AccountId && u.Revision == request.Revision)
            .ExecuteUpdateAsync(setter => setter
                .SetProperty(u => u.Status, UserStatus.Suspended)
                .SetProperty(u => u.TokenSecurityVersion, u => u.TokenSecurityVersion + 1)
                .SetProperty(u => u.Revision, u => u.Revision + 1)
                .SetProperty(u => u.TerminationPending, hasUnfinishedGames)
                .SetProperty(u => u.UpdatedAt, now)
                .SetProperty(u => u.UpdatedBy, adminId), cancellationToken);

        // Concurrency Guard Check - Verifies row was modified under matched revision fence
        if (updatedCount == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure<SuspendUserResult>(AccountErrors.ConcurrentModification);
        }

        // Phase 1 Commit - Atomically persists suspended cutoff state prior to background sweep or socket eviction
        await transaction.CommitAsync(cancellationToken);

        // Phase 2 Asynchronous Handoff (ACCT-SUSP-004, ACCT-RISK-001) - Dispatches channel notification to background worker for bounded batch game termination
        if (hasUnfinishedGames)
        {
            _suspensionFinalizerChannel.NotifySuspension(request.AccountId);
        }

        // Cluster-Wide Socket Eviction - Severs active SignalR connections for host and connected game sessions within p95 <= 500ms (ACCT-SLO-002)
        await _socketEvictionService.EvictUserSocketsAsync(request.AccountId, cancellationToken);

        return Result.Success(new SuspendUserResult(hasUnfinishedGames));
    }
}
