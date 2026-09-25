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
    private static readonly TimeSpan RaceGracePeriod = TimeSpan.FromSeconds(10);

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
        if (string.IsNullOrWhiteSpace(request.RawRefreshToken) || request.RawRefreshToken.Length > 128)
        {
            return Result.Failure<RefreshResult>(AuthErrors.InvalidRefreshToken);
        }

        byte[] presentedTokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(request.RawRefreshToken));
        var presentedToken = await _dbContext.RefreshTokens
            .AsNoTracking()
            .SingleOrDefaultAsync(token => token.TokenHash == presentedTokenHash, cancellationToken);

        if (presentedToken is null)
        {
            return Result.Failure<RefreshResult>(AuthErrors.InvalidRefreshToken);
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Credential mutations lock the account row first. Re-read the token after
        // acquiring it so revocation and rotation have one database commit order.
        var user = await _dbContext.GetUserForUpdateAsync(presentedToken.UserId, cancellationToken);
        if (user is null || user.Status != UserStatus.Active)
        {
            return Result.Failure<RefreshResult>(AuthErrors.InvalidRefreshToken);
        }

        var token = await _dbContext.RefreshTokens
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == presentedToken.Id, cancellationToken);

        if (token is null || token.RevokedAt.HasValue)
        {
            return Result.Failure<RefreshResult>(AuthErrors.InvalidRefreshToken);
        }

        var now = _timeProvider.GetUtcNow();
        if (token.RotatedAt.HasValue)
        {
            if (now - token.RotatedAt.Value < RaceGracePeriod)
            {
                return Result.Failure<RefreshResult>(AuthErrors.RefreshRace);
            }

            await RevokeFamilyAndIncrementSecurityVersionAsync(token, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Failure<RefreshResult>(AuthErrors.RefreshTokenReuse);
        }

        var refreshTokenOptions = _refreshTokenOptions.Value;
        if (now >= token.ExpiresAt ||
            now >= token.FamilyCreatedAt.AddDays(refreshTokenOptions.FamilyMaxLifetimeDays))
        {
            return Result.Failure<RefreshResult>(AuthErrors.InvalidRefreshToken);
        }

        int consumed = await _dbContext.RefreshTokens
            .Where(candidate => candidate.Id == token.Id && candidate.RotatedAt == null && candidate.RevokedAt == null)
            .ExecuteUpdateAsync(setter => setter.SetProperty(candidate => candidate.RotatedAt, now), cancellationToken);

        if (consumed != 1)
        {
            return Result.Failure<RefreshResult>(AuthErrors.InvalidRefreshToken);
        }

        string replacementRawToken = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(RefreshTokenEntropyBytes));
        var replacementToken = new RefreshToken
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

        _dbContext.RefreshTokens.Add(replacementToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

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
        RefreshToken token,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await _dbContext.RefreshTokens
            .Where(candidate => candidate.UserId == token.UserId &&
                                candidate.TokenFamilyId == token.TokenFamilyId &&
                                candidate.RevokedAt == null)
            .ExecuteUpdateAsync(setter => setter.SetProperty(candidate => candidate.RevokedAt, now), cancellationToken);

        await _dbContext.Users
            .Where(candidate => candidate.Id == token.UserId)
            .ExecuteUpdateAsync(setter => setter
                .SetProperty(candidate => candidate.TokenSecurityVersion, candidate => candidate.TokenSecurityVersion + 1)
                .SetProperty(candidate => candidate.UpdatedAt, now), cancellationToken);
    }
}
