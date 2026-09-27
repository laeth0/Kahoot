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
        // State: Caller authorization check (must be an authenticated SystemAdmin)
        if (!_currentUser.IsAuthenticated || !string.Equals(_currentUser.Role, nameof(UserRole.SystemAdmin), StringComparison.Ordinal))
        {
            return Result.Failure(AuthErrors.Forbidden);
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        Guid? adminId = _currentUser.UserId;

        // Step: Acquire pessimistic row lock (SELECT FOR UPDATE) inside transaction to serialize mutation
        await using IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        User? user = await _dbContext.GetUserForUpdateAsync(request.AccountId, cancellationToken);

        // State: Target account validation (must exist and belong to Host role)
        if (user is null || user.Role != UserRole.Host)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(AccountErrors.NotFound);
        }

        // State: Outcome B - Idempotent replay when previous commit succeeded but client dropped response
        if (user.Status == UserStatus.Active && user.Revision == request.Revision + 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Success();
        }

        // State: Optimistic Concurrency Control (OCC) - Detect concurrent modification or stale revision
        if (user.Revision != request.Revision)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(AccountErrors.ConcurrentModification);
        }

        // State: Idempotent no-op - Account is already active under the requested revision
        if (user.Status == UserStatus.Active)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Success();
        }

        // State: Reactivation Gate (ACCT-BOUND-005, ACCT-ERR-007) - Block reactivation while Phase 2 finalization is in-flight
        if (user.TerminationPending)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(AccountErrors.TerminationPending);
        }

        // Step: Atomic user update - Activate status, advance revision and audit metadata
        int updatedCount = await _dbContext.Users
            .Where(u => u.Id == request.AccountId && u.Revision == request.Revision)
            .ExecuteUpdateAsync(setter => setter
                .SetProperty(u => u.Status, UserStatus.Active)
                .SetProperty(u => u.Revision, u => u.Revision + 1)
                .SetProperty(u => u.UpdatedAt, now)
                .SetProperty(u => u.UpdatedBy, adminId), cancellationToken);

        // State: Concurrency check - Verify user row was updated without intervening changes
        if (updatedCount == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(AccountErrors.ConcurrentModification);
        }

        // Step: Commit reactivation transaction
        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
