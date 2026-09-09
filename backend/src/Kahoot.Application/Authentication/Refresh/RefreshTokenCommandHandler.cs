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

        RefreshToken? stored = await dbContext.RefreshTokens
            .Include(token => token.Host)
            .FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

        if (stored?.Host is null)
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
            stored.RevokedAt = now.UtcDateTime;
            await dbContext.SaveChangesAsync(cancellationToken);
            return Result.Failure<AuthenticationResponse>(AuthenticationErrors.InvalidRefreshToken);
        }

        (RefreshToken rotated, AuthenticationResponse response) = AuthTokenFactory.Issue(
            stored.Host,
            jwtTokenService,
            secureTokenGenerator,
            tokenHasher,
            jwtOptions.Value.RefreshTokenDays,
            now);

        stored.RevokedAt = now.UtcDateTime;
        stored.ReplacedByTokenId = rotated.Id;
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
