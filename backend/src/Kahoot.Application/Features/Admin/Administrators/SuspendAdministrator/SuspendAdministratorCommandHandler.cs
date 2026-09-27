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

namespace Kahoot.Application.Features.Admin.Administrators.SuspendAdministrator;

public sealed class SuspendAdministratorCommandHandler : ICommandHandler<SuspendAdministratorCommand>
{
    private readonly IAppDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;
    private readonly ISocketEvictionService _socketEvictionService;

    public SuspendAdministratorCommandHandler(
        IAppDbContext dbContext,
        ICurrentUser currentUser,
        TimeProvider timeProvider,
        ISocketEvictionService socketEvictionService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
        _socketEvictionService = socketEvictionService;
    }

    public async Task<Result> Handle(
        SuspendAdministratorCommand request,
        CancellationToken cancellationToken)
    {
        // State: Caller authorization check (must be an authenticated SystemAdmin)
        if (!_currentUser.IsAuthenticated || !string.Equals(_currentUser.Role, nameof(UserRole.SystemAdmin), StringComparison.Ordinal))
        {
            return Result.Failure(AuthErrors.Forbidden);
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        Guid? adminId = _currentUser.UserId;

        // Step: Acquire transaction to serialize the admin suspension and active count verification
        await using IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Step: Acquire pessimistic row locks (SELECT FOR UPDATE) on all currently active SystemAdmin rows ordered by ID
        List<User> activeAdmins = await _dbContext.GetActiveAdministratorsForUpdateAsync(cancellationToken);

        User? targetUser = activeAdmins.FirstOrDefault(user => user.Id == request.AdministratorId);

        if (targetUser is null)
        {
            targetUser = await _dbContext.GetUserForUpdateAsync(request.AdministratorId, cancellationToken);

            // State: Target administrator validation (must exist and belong to SystemAdmin role)
            if (targetUser is null || targetUser.Role != UserRole.SystemAdmin)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure(AccountErrors.NotFound);
            }

            // State: Outcome B - Idempotent replay when previous commit succeeded but client dropped response
            if (targetUser.Status == UserStatus.Suspended && targetUser.Revision == request.Revision + 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Success();
            }

            // State: Optimistic Concurrency Control (OCC) - Detect concurrent modification or stale revision
            if (targetUser.Revision != request.Revision)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure(AccountErrors.ConcurrentModification);
            }

            // State: Idempotent no-op - Administrator is already suspended under the requested revision
            if (targetUser.Status == UserStatus.Suspended)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Success();
            }
        }

        // State: Optimistic Concurrency Control (OCC) - Detect concurrent modification or stale revision
        if (targetUser.Revision != request.Revision)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(AccountErrors.ConcurrentModification);
        }

        // State: Last-Active-Administrator Invariant (ACCT-ADMIN-003, ACCT-BOUND-004) - Transactionally prevent suspending final admin
        if (activeAdmins.Count <= 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(AccountErrors.LastAdministrator);
        }

        // Step: Revoke all active refresh tokens for the target administrator
        await _dbContext.RefreshTokens
            .Where(token => token.UserId == request.AdministratorId && token.RevokedAt == null)
            .ExecuteUpdateAsync(setter => setter.SetProperty(t => t.RevokedAt, now), cancellationToken);

        // Step: Atomic user update - Suspend status, increment TokenSecurityVersion (JWT cutoff), advance revision
        int updatedCount = await _dbContext.Users
            .Where(u => u.Id == request.AdministratorId && u.Revision == request.Revision)
            .ExecuteUpdateAsync(setter => setter
                .SetProperty(u => u.Status, UserStatus.Suspended)
                .SetProperty(u => u.TokenSecurityVersion, u => u.TokenSecurityVersion + 1)
                .SetProperty(u => u.Revision, u => u.Revision + 1)
                .SetProperty(u => u.UpdatedAt, now)
                .SetProperty(u => u.UpdatedBy, adminId), cancellationToken);

        // State: Concurrency check - Verify user row was updated without intervening changes
        if (updatedCount == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(AccountErrors.ConcurrentModification);
        }

        // Step: Commit administrator suspension transaction
        await transaction.CommitAsync(cancellationToken);

        // Step: Evict active socket connections for the suspended administrator
        await _socketEvictionService.EvictUserSocketsAsync(request.AdministratorId, cancellationToken);

        return Result.Success();
    }
}
