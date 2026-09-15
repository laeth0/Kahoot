using Kahoot.Application.Common.Abstractions;
using Kahoot.Domain.Hosts;

namespace Kahoot.Application.Authentication.Common;

internal static class AuthTokenFactory
{
    public static (RefreshToken RefreshToken, AuthenticationResponse Response) Issue(
        Guid hostId,
        string username,
        IJwtTokenService jwtTokenService,
        ISecureTokenGenerator secureTokenGenerator,
        ITokenHasher tokenHasher,
        int refreshTokenDays,
        DateTimeOffset now,
        Guid? familyId = null)
    {
        AccessTokenResult accessToken = jwtTokenService.CreateAccessToken(hostId, username);

        string rawRefreshToken = secureTokenGenerator.GenerateToken();
        DateTimeOffset refreshTokenExpiresAt = now.AddDays(refreshTokenDays);

        RefreshToken refreshToken = new()
        {
            FamilyId = familyId ?? Guid.CreateVersion7(),
            HostId = hostId,
            TokenHash = tokenHasher.Hash(rawRefreshToken),
            CreatedAt = now.UtcDateTime,
            ExpiresAt = refreshTokenExpiresAt.UtcDateTime
        };

        AuthenticationResponse response = new(
            hostId,
            username,
            accessToken.Token,
            accessToken.ExpiresAt,
            rawRefreshToken,
            refreshTokenExpiresAt);

        return (refreshToken, response);
    }
}
