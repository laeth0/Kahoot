using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Kahoot.Application.Common;
using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Options;
using Kahoot.Application.Common.Persistence;
using Kahoot.Application.Common.Results;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;

namespace Kahoot.Application.Features.Auth.Refresh;

public sealed class RefreshCommandHandler : ICommandHandler<RefreshCommand, RefreshResult>
{
    private const int RefreshTokenEntropyBytes = 32;
    private static readonly TimeSpan RaceGracePeriod = TimeSpan.FromSeconds(10);

    private readonly IAppDbContext _dbContext;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly ISocketEvictionService _socketEvictionService;
    private readonly IOptions<RefreshTokenOptions> _refreshTokenOptions;
    private readonly TimeProvider _timeProvider;

    public RefreshCommandHandler(
        IAppDbContext dbContext,
        IJwtTokenGenerator jwtTokenGenerator,
        ISocketEvictionService socketEvictionService,
        IOptions<RefreshTokenOptions> refreshTokenOptions,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _jwtTokenGenerator = jwtTokenGenerator;
        _socketEvictionService = socketEvictionService;
        _refreshTokenOptions = refreshTokenOptions;
        _timeProvider = timeProvider;
    }

    public async Task<Result<RefreshResult>> Handle(
        RefreshCommand request,
        CancellationToken cancellationToken)
    {
        // Spec Compliance (AUTH-ERR-003) - Malformed or missing refresh tokens return 401 Auth.InvalidRefreshToken rather than 400 validation failure
        if (string.IsNullOrWhiteSpace(request.RawRefreshToken) || request.RawRefreshToken.Length > 128)
        {
            return Result.Failure<RefreshResult>(AuthErrors.InvalidRefreshToken);
        }

        byte[] presentedTokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(request.RawRefreshToken));

        // Query Performance: AsNoTracking() eliminates tracking overhead; TokenHash unique index avoids table scan
        RefreshToken? presentedToken = await _dbContext.RefreshTokens
            .AsNoTracking()
            .SingleOrDefaultAsync(token => token.TokenHash == presentedTokenHash, cancellationToken);

        if (presentedToken is null)
        {
            return Result.Failure<RefreshResult>(AuthErrors.InvalidRefreshToken);
        }

        await using IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Security & Concurrency: GetUserForUpdateAsync acquires a pessimistic row lock (SELECT FOR UPDATE) to coordinate revocation and rotation commit order
        User? user = await _dbContext.GetUserForUpdateAsync(presentedToken.UserId, cancellationToken);
        if (user is null || user.Status != UserStatus.Active)
        {
            return Result.Failure<RefreshResult>(AuthErrors.InvalidRefreshToken);
        }

        // Query Performance: AsNoTracking() re-reads token state inside locked transaction boundary to verify state freshness
        RefreshToken? token = await _dbContext.RefreshTokens
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == presentedToken.Id, cancellationToken);

        if (token is null || token.RevokedAt.HasValue)
        {
            return Result.Failure<RefreshResult>(AuthErrors.InvalidRefreshToken);
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        if (token.RotatedAt.HasValue)
        {
            // Multi-Tab Race Grace Window - Allows 10s leeway for parallel browser tab refreshes without invalidating session
            if (now - token.RotatedAt.Value < RaceGracePeriod)
            {
                return Result.Failure<RefreshResult>(AuthErrors.RefreshRace);
            }

            // Malicious Reuse Detection - Replaying a consumed token after grace period triggers full family revocation
            await RevokeFamilyAndIncrementSecurityVersionAsync(token, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            // Post-Commit Broadcast: Evicts active sockets cluster-wide upon detecting malicious token replay
            await _socketEvictionService.EvictUserSocketsAsync(user.Id, cancellationToken);

            return Result.Failure<RefreshResult>(AuthErrors.RefreshTokenReuse);
        }

        // Sliding & Absolute Lifetime Bounds - Enforces both per-token sliding window (14d) and hard family lifetime cap (30d)
        RefreshTokenOptions refreshTokenOptions = _refreshTokenOptions.Value;
        if (now >= token.ExpiresAt ||
            now >= token.FamilyCreatedAt.AddDays(refreshTokenOptions.FamilyMaxLifetimeDays))
        {
            return Result.Failure<RefreshResult>(AuthErrors.InvalidRefreshToken);
        }

        // Single-Use Rotation (RTR) & Performance: ExecuteUpdateAsync issues an atomic single-statement SQL UPDATE without loading entities into memory
        int consumed = await _dbContext.RefreshTokens
            .Where(candidate => candidate.Id == token.Id && candidate.RotatedAt == null && candidate.RevokedAt == null)
            .ExecuteUpdateAsync(setter => setter.SetProperty(candidate => candidate.RotatedAt, now), cancellationToken);

        if (consumed != 1)
        {
            return Result.Failure<RefreshResult>(AuthErrors.InvalidRefreshToken);
        }

        // Cryptographic Token Generation - Generates high-entropy replacement token in same lineage family
        string replacementRawToken = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(RefreshTokenEntropyBytes));
        RefreshToken replacementToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenFamilyId = token.TokenFamilyId,
            FamilyCreatedAt = token.FamilyCreatedAt,
            TokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(replacementRawToken)),
            CreatedAt = now,
            ExpiresAt = now.AddDays(refreshTokenOptions.LifetimeDays),
            RotatedAt = null,
            RevokedAt = null
        };

        // Session Persistence: Persists replacement refresh token under active row lock
        _dbContext.RefreshTokens.Add(replacementToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        AccessTokenResult accessToken = _jwtTokenGenerator.GenerateAccessToken(user);
        await transaction.CommitAsync(cancellationToken);

        RefreshResponse response = new RefreshResponse(
            user.Id,
            user.DisplayUsername,
            user.Role.ToString(),
            accessToken.Token,
            accessToken.ExpiresInSeconds);

        return Result.Success(new RefreshResult(response, replacementRawToken));
    }

    // Session Family Revocation - Atomically revokes all family tokens and increments user security version
    private async Task RevokeFamilyAndIncrementSecurityVersionAsync(
        RefreshToken token,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // Performance: ExecuteUpdateAsync bulk-revokes all active tokens in the family in a single SQL UPDATE
        await _dbContext.RefreshTokens
            .Where(candidate => candidate.UserId == token.UserId &&
                                candidate.TokenFamilyId == token.TokenFamilyId &&
                                candidate.RevokedAt == null)
            .ExecuteUpdateAsync(setter => setter.SetProperty(candidate => candidate.RevokedAt, now), cancellationToken);

        // Security & Performance: Increments TokenSecurityVersion via ExecuteUpdateAsync to immediately invalidate outstanding JWTs across all nodes
        await _dbContext.Users
            .Where(candidate => candidate.Id == token.UserId)
            .ExecuteUpdateAsync(setter => setter
                .SetProperty(candidate => candidate.TokenSecurityVersion, candidate => candidate.TokenSecurityVersion + 1)
                .SetProperty(candidate => candidate.UpdatedAt, now), cancellationToken);
    }
}
