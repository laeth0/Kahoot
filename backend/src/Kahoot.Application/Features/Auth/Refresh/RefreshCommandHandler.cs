using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Options;
using Kahoot.Application.Common.Persistence;
using Kahoot.Application.Common.Results;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kahoot.Application.Features.Auth.Refresh;

public sealed class RefreshCommandHandler : ICommandHandler<RefreshCommand, RefreshResult>
{
    private const int RefreshTokenEntropyBytes = 32;

    private readonly IAppDbContext _dbContext;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IOptions<RefreshTokenOptions> _refreshTokenOptions;
    private readonly TimeProvider _timeProvider;

    public RefreshCommandHandler(
        IAppDbContext dbContext,
        IJwtTokenGenerator jwtTokenGenerator,
        IOptions<RefreshTokenOptions> refreshTokenOptions,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _jwtTokenGenerator = jwtTokenGenerator;
        _refreshTokenOptions = refreshTokenOptions;
        _timeProvider = timeProvider;
    }

    public async Task<Result<RefreshResult>> Handle(
        RefreshCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RawRefreshToken))
        {
            return Result.Failure<RefreshResult>(AuthErrors.InvalidRefreshToken);
        }

        var now = _timeProvider.GetUtcNow();

        // 1. Hash the presented raw refresh token using SHA-256 (raw token is never queried or stored)
        byte[] presentedTokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(request.RawRefreshToken));

        // 2. Indexed lookup by TokenHash
        var token = await _dbContext.RefreshTokens
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.TokenHash == presentedTokenHash, cancellationToken);

        if (token is null)
        {
            return Result.Failure<RefreshResult>(AuthErrors.InvalidRefreshToken);
        }

        // 3. Replay evaluation on already-consumed token
        if (token.RotatedAt.HasValue)
        {
            var delta = now - token.RotatedAt.Value;

            // Grace window (< 10s): Concurrent browser tab / lost response race -> 409
            if (delta < TimeSpan.FromSeconds(10))
            {
                return Result.Failure<RefreshResult>(AuthErrors.RefreshRace);
            }

            // Reuse detection (>= 10s): Malicious replay -> revoke family, increment security version, 401
            await RevokeFamilyAndIncrementSecurityVersionAsync(token.UserId, token.TokenFamilyId, now, cancellationToken);
            return Result.Failure<RefreshResult>(AuthErrors.RefreshTokenReuse);
        }

        // 4. Validate revocation status
        if (token.RevokedAt.HasValue)
        {
            return Result.Failure<RefreshResult>(AuthErrors.InvalidRefreshToken);
        }

        // 5. Validate expiration and absolute family cap (configured in RefreshTokenOptions)
        var refreshTokenOptions = _refreshTokenOptions.Value;
        if (now >= token.ExpiresAt ||
            now >= token.FamilyCreatedAt.AddDays(refreshTokenOptions.FamilyMaxLifetimeDays))
        {
            return Result.Failure<RefreshResult>(AuthErrors.InvalidRefreshToken);
        }

        // 6. Load user to validate active status and obtain claims for new JWT
        var user = await _dbContext.Users
            .SingleOrDefaultAsync(candidate => candidate.Id == token.UserId, cancellationToken);

        if (user is null || user.Status != UserStatus.Active)
        {
            return Result.Failure<RefreshResult>(AuthErrors.InvalidRefreshToken);
        }

        // 7. Atomic rotation within a database transaction
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Database-level concurrency gate:
        // Atomic conditional update ensures only one in-flight request can successfully consume the token.
        int affectedRows = await _dbContext.RefreshTokens
            .Where(candidate => candidate.Id == token.Id && candidate.RotatedAt == null && candidate.RevokedAt == null)
            .ExecuteUpdateAsync(setter => setter.SetProperty(t => t.RotatedAt, now), cancellationToken);

        if (affectedRows == 0)
        {
            // Another concurrent request won the rotation race.
            await transaction.RollbackAsync(cancellationToken);

            var latestToken = await _dbContext.RefreshTokens
                .AsNoTracking()
                .SingleOrDefaultAsync(candidate => candidate.Id == token.Id, cancellationToken);

            var delta = latestToken?.RotatedAt is not null
                ? now - latestToken.RotatedAt.Value
                : TimeSpan.Zero;

            if (delta < TimeSpan.FromSeconds(10))
            {
                return Result.Failure<RefreshResult>(AuthErrors.RefreshRace);
            }

            await RevokeFamilyAndIncrementSecurityVersionAsync(token.UserId, token.TokenFamilyId, now, cancellationToken);
            return Result.Failure<RefreshResult>(AuthErrors.RefreshTokenReuse);
        }

        // 8. Generate replacement refresh token; persist only SHA256(replacementRawToken)
        byte[] randomBytes = RandomNumberGenerator.GetBytes(RefreshTokenEntropyBytes);
        string replacementRawToken = Base64Url.EncodeToString(randomBytes);
        byte[] replacementTokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(replacementRawToken));

        var replacementToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenFamilyId = token.TokenFamilyId,
            FamilyCreatedAt = token.FamilyCreatedAt,
            TokenHash = replacementTokenHash,
            CreatedAt = now,
            ExpiresAt = now.AddDays(refreshTokenOptions.LifetimeDays),
            RotatedAt = null,
            RevokedAt = null
        };

        _dbContext.RefreshTokens.Add(replacementToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // 9. Generate new access JWT
        var accessToken = _jwtTokenGenerator.GenerateAccessToken(user);

        await transaction.CommitAsync(cancellationToken);

        var response = new RefreshResponse(
            user.Id,
            user.DisplayUsername,
            user.Role.ToString(),
            accessToken.Token,
            accessToken.ExpiresInSeconds);

        return Result.Success(new RefreshResult(response, replacementRawToken));
    }

    private async Task RevokeFamilyAndIncrementSecurityVersionAsync(
        Guid userId,
        Guid tokenFamilyId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        int revokedTokens = await _dbContext.RefreshTokens
            .Where(candidate => candidate.TokenFamilyId == tokenFamilyId && candidate.RevokedAt == null)
            .ExecuteUpdateAsync(setter => setter.SetProperty(t => t.RevokedAt, now), cancellationToken);

        if (revokedTokens > 0)
        {
            await _dbContext.Users
                .Where(candidate => candidate.Id == userId)
                .ExecuteUpdateAsync(setter => setter
                    .SetProperty(u => u.TokenSecurityVersion, u => u.TokenSecurityVersion + 1)
                    .SetProperty(u => u.UpdatedAt, now), cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }
}
