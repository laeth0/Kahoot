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

namespace Kahoot.Application.Features.Admin.Administrators.ReactivateAdministrator;

public sealed class ReactivateAdministratorCommandHandler : ICommandHandler<ReactivateAdministratorCommand>
{
    private readonly IAppDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;

    public ReactivateAdministratorCommandHandler(
        IAppDbContext dbContext,
        ICurrentUser currentUser,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<Result> Handle(
        ReactivateAdministratorCommand request,
        CancellationToken cancellationToken)
    {
        // Administrative Role Authorization - Restricts administrator status mutations strictly to authenticated SystemAdmin callers (ACCT-SEC-001)
        if (!_currentUser.IsAuthenticated || !string.Equals(_currentUser.Role, nameof(UserRole.SystemAdmin), StringComparison.Ordinal))
        {
            return Result.Failure(AuthErrors.Forbidden);
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        Guid? adminId = _currentUser.UserId;

        // Transactional Consistency Boundary - Serializes administrator reactivation to prevent race conditions during lifecycle mutations
        await using IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Pessimistic Row Lock (SELECT FOR UPDATE) - Locks target administrator row to serialize concurrent status updates
        User? user = await _dbContext.GetUserForUpdateAsync(request.AdministratorId, cancellationToken);

        // Role & Entity Invariant Check - Verifies user exists and belongs strictly to the SystemAdmin role (ACCT-ERR-004)
        if (user is null || user.Role != UserRole.SystemAdmin)
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

        // Idempotent No-Op - Fast path if administrator is already active under current revision
        if (user.Status == UserStatus.Active)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Success();
        }

        // Atomic Status Transition - Transitions status to Active, increments monotonic revision, and sets audit timestamps
        int updatedCount = await _dbContext.Users
            .Where(u => u.Id == request.AdministratorId && u.Revision == request.Revision)
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

        // Durable Transaction Commit - Persists active administrator state changes
        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
