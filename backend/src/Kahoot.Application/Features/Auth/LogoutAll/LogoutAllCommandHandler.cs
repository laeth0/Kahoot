using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Persistence;
using Kahoot.Application.Common.Results;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Kahoot.Application.Features.Auth.LogoutAll;

public sealed class LogoutAllCommandHandler : ICommandHandler<LogoutAllCommand>
{
    private readonly IAppDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly ISocketEvictionService _socketEvictionService;
    private readonly TimeProvider _timeProvider;

    public LogoutAllCommandHandler(
        IAppDbContext dbContext,
        ICurrentUser currentUser,
        ISocketEvictionService socketEvictionService,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _socketEvictionService = socketEvictionService;
        _timeProvider = timeProvider;
    }

    public async Task<Result> Handle(
        LogoutAllCommand request,
        CancellationToken cancellationToken)
    {
        Guid? userId = _currentUser.UserId;
        if (!userId.HasValue || userId.Value == Guid.Empty)
        {
            return Result.Failure(AuthErrors.Unauthorized);
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();

        await using IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Security & Concurrency: GetUserForUpdateAsync acquires a pessimistic row lock (SELECT FOR UPDATE) to serialize account updates
        User? user = await _dbContext.GetUserForUpdateAsync(userId.Value, cancellationToken);
        if (user is null || user.Status != UserStatus.Active)
        {
            return Result.Failure(AuthErrors.Unauthorized);
        }

        // Bulk Session Revocation & Performance - ExecuteUpdateAsync issues a direct SQL UPDATE across all sessions without change-tracker memory overhead
        await _dbContext.RefreshTokens
            .Where(token => token.UserId == userId.Value && token.RevokedAt == null)
            .ExecuteUpdateAsync(setter => setter.SetProperty(t => t.RevokedAt, now), cancellationToken);

        // Stateless JWT Invalidation (TokenSecurityVersion) - Increments version via ExecuteUpdateAsync so nodes reject existing JWTs without a distributed blacklist
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

        // Post-Commit Broadcast: Evicts active SignalR sockets across the entire cluster after global session state is committed durable
        await _socketEvictionService.EvictUserSocketsAsync(userId.Value, cancellationToken);

        return Result.Success();
    }
}
