using Kahoot.Application.Authentication.Common;
using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Messaging;
using Kahoot.Domain.Common;
using Kahoot.Domain.Hosts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kahoot.Application.Authentication.Refresh;

internal sealed class RefreshTokenCommandHandler(
    IApplicationDbContext dbContext,
    IJwtTokenService jwtTokenService,
    ISecureTokenGenerator secureTokenGenerator,
    ITokenHasher tokenHasher,
    IOptions<JwtOptions> jwtOptions,
    TimeProvider timeProvider) : ICommandHandler<RefreshTokenCommand, AuthenticationResponse>
{
    public async Task<Result<AuthenticationResponse>> Handle(
        RefreshTokenCommand command,
        CancellationToken cancellationToken)
    {
        string tokenHash = tokenHasher.Hash(command.RefreshToken);
        DateTimeOffset now = timeProvider.GetUtcNow();

        var stored = await dbContext.RefreshTokens
            .AsNoTracking()
            .Where(token => token.TokenHash == tokenHash)
            .Select(token => new
            {
                token.Id,
                token.HostId,
                token.ExpiresAt,
                token.RevokedAt,
                Username = token.Host!.Username
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (stored is null)
        {
            return Result.Failure<AuthenticationResponse>(AuthenticationErrors.InvalidRefreshToken);
        }

        if (stored.RevokedAt is not null)
        {
            await RevokeChainAsync(stored.HostId, now, cancellationToken);
            return Result.Failure<AuthenticationResponse>(AuthenticationErrors.RefreshTokenReuseDetected);
        }

        if (stored.ExpiresAt <= now.UtcDateTime)
        {
            await dbContext.RefreshTokens
                .Where(token => token.Id == stored.Id && token.RevokedAt == null)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(token => token.RevokedAt, now.UtcDateTime),
                    cancellationToken);

            return Result.Failure<AuthenticationResponse>(AuthenticationErrors.InvalidRefreshToken);
        }

        (RefreshToken rotated, AuthenticationResponse response) = AuthTokenFactory.Issue(
            stored.HostId,
            stored.Username,
            jwtTokenService,
            secureTokenGenerator,
            tokenHasher,
            jwtOptions.Value.RefreshTokenDays,
            now);

        int rotatedRows = await dbContext.RefreshTokens
            .Where(token => token.Id == stored.Id && token.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(token => token.RevokedAt, now.UtcDateTime)
                    .SetProperty(token => token.ReplacedByTokenId, rotated.Id),
                cancellationToken);

        if (rotatedRows == 0)
        {
            return Result.Failure<AuthenticationResponse>(AuthenticationErrors.InvalidRefreshToken);
        }

        dbContext.RefreshTokens.Add(rotated);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(response);
    }

    private async Task RevokeChainAsync(Guid hostId, DateTimeOffset now, CancellationToken cancellationToken) =>
        await dbContext.RefreshTokens
            .Where(token => token.HostId == hostId && token.RevokedAt == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(token => token.RevokedAt, now.UtcDateTime),
                cancellationToken);
}
