using Kahoot.Application.Common.Abstractions;
using Kahoot.Domain.Hosts;

namespace Kahoot.Application.Authentication.Common;

internal static class AuthTokenFactory
{
    public static (RefreshToken RefreshToken, AuthenticationResponse Response) Issue(
        Host host,
        IJwtTokenService jwtTokenService,
        ISecureTokenGenerator secureTokenGenerator,
        ITokenHasher tokenHasher,
        int refreshTokenDays,
        DateTimeOffset now)
    {
        AccessTokenResult accessToken = jwtTokenService.CreateAccessToken(host.Id, host.Username);

        string rawRefreshToken = secureTokenGenerator.GenerateToken();
        DateTimeOffset refreshTokenExpiresAt = now.AddDays(refreshTokenDays);

        RefreshToken refreshToken = new()
        {
            HostId = host.Id,
            TokenHash = tokenHasher.Hash(rawRefreshToken),
            ExpiresAt = refreshTokenExpiresAt.UtcDateTime
        };

        AuthenticationResponse response = new(
            host.Id,
            host.Username,
            accessToken.Token,
            accessToken.ExpiresAt,
            rawRefreshToken,
            refreshTokenExpiresAt);

        return (refreshToken, response);
    }
}
