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

namespace Kahoot.Application.Features.Admin.Users.ReactivateUser;

public sealed class ReactivateUserCommandHandler : ICommandHandler<ReactivateUserCommand>
{
    private readonly IAppDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;

    public ReactivateUserCommandHandler(
        IAppDbContext dbContext,
        ICurrentUser currentUser,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<Result> Handle(
        ReactivateUserCommand request,
        CancellationToken cancellationToken)
    {
        // Administrative Role Authorization - Restricts host account reactivation strictly to authenticated SystemAdmin callers (ACCT-SEC-001)
        if (!_currentUser.IsAuthenticated || !string.Equals(_currentUser.Role, nameof(UserRole.SystemAdmin), StringComparison.Ordinal))
        {
            return Result.Failure(AuthErrors.Forbidden);
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        Guid? adminId = _currentUser.UserId;

        // Transactional Consistency Boundary - Serializes host reactivation mutation to prevent concurrent lifecycle races
        await using IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Pessimistic Row Lock (SELECT FOR UPDATE) - Locks target host account row to serialize concurrent lifecycle changes
        User? user = await _dbContext.GetUserForUpdateAsync(request.AccountId, cancellationToken);

        // Role & Entity Invariant Check - Verifies account exists and belongs strictly to the Host role (ACCT-ERR-004)
        if (user is null || user.Role != UserRole.Host)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(AccountErrors.NotFound);
        }

        // Idempotent Replay (Outcome B) - Returns success if previous commit succeeded but network response was dropped
        if (user.Status == UserStatus.Active && user.Revision == request.Revision + 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Success();
        }

        // Optimistic Concurrency Control (OCC) - Detects state drift or concurrent modifications via revision mismatch (ACCT-BOUND-001, ACCT-ERR-005)
        if (user.Revision != request.Revision)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(AccountErrors.ConcurrentModification);
        }

        // Idempotent No-Op - Fast path if account is already active under current revision
        if (user.Status == UserStatus.Active)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Success();
        }

        // Reactivation Gate Barrier (ACCT-BOUND-005, ACCT-ERR-007, ACCT-RISK-005) - Blocks reactivation while Phase 2 background game finalization is in-flight
        if (user.TerminationPending)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(AccountErrors.TerminationPending);
        }

        // Atomic Status Transition - Transitions status to Active, increments monotonic revision, and sets audit timestamps
        int updatedCount = await _dbContext.Users
            .Where(u => u.Id == request.AccountId && u.Revision == request.Revision)
            .ExecuteUpdateAsync(setter => setter
                .SetProperty(u => u.Status, UserStatus.Active)
                .SetProperty(u => u.Revision, u => u.Revision + 1)
                .SetProperty(u => u.UpdatedAt, now)
                .SetProperty(u => u.UpdatedBy, adminId), cancellationToken);

        // Concurrency Guard Check - Verifies row was modified under matched revision fence
        if (updatedCount == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(AccountErrors.ConcurrentModification);
        }

        // Durable Transaction Commit - Persists active host state changes
        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
