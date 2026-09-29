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
        // Administrative Role Authorization - Restricts administrator suspension strictly to authenticated SystemAdmin callers (ACCT-SEC-001)
        if (!_currentUser.IsAuthenticated || !string.Equals(_currentUser.Role, nameof(UserRole.SystemAdmin), StringComparison.Ordinal))
        {
            return Result.Failure(AuthErrors.Forbidden);
        }

        Guid? adminId = _currentUser.UserId;

        // Transactional Consistency Boundary - Serializes admin suspension and active count verification to prevent split-brain lockout
        await using IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Pessimistic Row Lock & Deadlock Prevention - Acquires SELECT FOR UPDATE ordered by ID across active SystemAdmins to serialize count check (ACCT-ADMIN-003, ACCT-RISK-003)
        List<User> activeAdmins = await _dbContext.GetActiveAdministratorsForUpdateAsync(cancellationToken);

        User? targetUser = activeAdmins.FirstOrDefault(user => user.Id == request.AdministratorId);

        if (targetUser is null)
        {
            targetUser = await _dbContext.GetUserForUpdateAsync(request.AdministratorId, cancellationToken);

            // Role & Entity Invariant Check - Verifies user exists and belongs strictly to the SystemAdmin role (ACCT-ERR-004)
            if (targetUser is null || targetUser.Role != UserRole.SystemAdmin)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure(AccountErrors.NotFound);
            }

            // Idempotent Replay (Outcome B) - Returns success if previous commit succeeded but network response was dropped
            if (targetUser.Status == UserStatus.Suspended && targetUser.Revision == request.Revision + 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Success();
            }

            // Optimistic Concurrency Control (OCC) - Detects state drift or concurrent modifications via revision mismatch (ACCT-BOUND-001, ACCT-ERR-005)
            if (targetUser.Revision != request.Revision)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Failure(AccountErrors.ConcurrentModification);
            }

            // Idempotent No-Op - Fast path if administrator is already suspended under current revision
            if (targetUser.Status == UserStatus.Suspended)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result.Success();
            }
        }

        // Optimistic Concurrency Control (OCC) - Detects state drift or concurrent modifications via revision mismatch (ACCT-BOUND-001, ACCT-ERR-005)
        if (targetUser.Revision != request.Revision)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(AccountErrors.ConcurrentModification);
        }

        // Last-Active-Administrator Invariant - Transactionally prevents suspending the sole remaining active administrator (ACCT-ADMIN-003, ACCT-BOUND-004, ACCT-RISK-003)
        if (activeAdmins.Count <= 1)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(AccountErrors.LastAdministrator);
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();

        // In-Database Bulk Token Revocation - Immediately revokes all active refresh tokens without memory loading
        await _dbContext.RefreshTokens
            .Where(token => token.UserId == request.AdministratorId && token.RevokedAt == null)
            .ExecuteUpdateAsync(setter => setter.SetProperty(t => t.RevokedAt, now), cancellationToken);

        // Immediate Suspension Cutoff - Atomically transitions to Suspended and increments TokenSecurityVersion for instant cluster-wide JWT invalidation (ACCT-SLO-001)
        int updatedCount = await _dbContext.Users
            .Where(u => u.Id == request.AdministratorId && u.Revision == request.Revision)
            .ExecuteUpdateAsync(setter => setter
                .SetProperty(u => u.Status, UserStatus.Suspended)
                .SetProperty(u => u.TokenSecurityVersion, u => u.TokenSecurityVersion + 1)
                .SetProperty(u => u.Revision, u => u.Revision + 1)
                .SetProperty(u => u.UpdatedAt, now)
                .SetProperty(u => u.UpdatedBy, adminId), cancellationToken);

        // Concurrency Guard Check - Verifies row was modified under matched revision fence
        if (updatedCount == 0)
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(AccountErrors.ConcurrentModification);
        }

        // Durable Transaction Commit - Persists suspended administrator state prior to external side effects
        await transaction.CommitAsync(cancellationToken);

        // Cluster-Wide Socket Eviction - Sever active SignalR connections within p95 <= 500ms post-commit (ACCT-SLO-002)
        await _socketEvictionService.EvictUserSocketsAsync(request.AdministratorId, cancellationToken);

        return Result.Success();
    }
}
