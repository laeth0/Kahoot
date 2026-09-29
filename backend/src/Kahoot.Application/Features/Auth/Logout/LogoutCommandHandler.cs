using System.Security.Cryptography;
using System.Text;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Persistence;
using Kahoot.Application.Common.Results;
using Kahoot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Kahoot.Application.Features.Auth.Logout;

public sealed class LogoutCommandHandler : ICommandHandler<LogoutCommand>
{
    private readonly IAppDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public LogoutCommandHandler(
        IAppDbContext dbContext,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<Result> Handle(
        LogoutCommand request,
        CancellationToken cancellationToken)
    {
        // Enumeration-Resistant Logout - Returns success regardless of token presence/validity to prevent probing
        if (string.IsNullOrWhiteSpace(request.RawRefreshToken))
        {
            return Result.Success();
        }

        // Hash presented raw token using SHA-256 (raw secrets are never stored)
        byte[] presentedTokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(request.RawRefreshToken));

        // Query Performance: AsNoTracking() eliminates tracking overhead; SingleOrDefaultAsync performs indexed seek on TokenHash
        RefreshToken? token = await _dbContext.RefreshTokens
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.TokenHash == presentedTokenHash, cancellationToken);

        if (token is null)
        {
            return Result.Success();
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();

        await using IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Security & Concurrency: GetUserForUpdateAsync acquires a pessimistic row lock (SELECT FOR UPDATE) to ensure account exists and serialize changes
        User? user = await _dbContext.GetUserForUpdateAsync(token.UserId, cancellationToken);
        if (user is null)
        {
            return Result.Success();
        }

        // Query Performance & Soft Revocation: ExecuteUpdateAsync revokes active family tokens in a single SQL statement; retains records for 7-day forensic window
        await _dbContext.RefreshTokens
            .Where(candidate => candidate.UserId == token.UserId &&
                                candidate.TokenFamilyId == token.TokenFamilyId &&
                                candidate.RevokedAt == null)
            .ExecuteUpdateAsync(setter => setter.SetProperty(t => t.RevokedAt, now), cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
