using System.Security.Claims;
using System.Text;
using Kahoot.Application.Authentication.Common;
using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Kahoot.Infrastructure.Authentication;

public sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider timeProvider)
    : IJwtTokenService, ISingletonService
{
    public AccessTokenResult CreateAccessToken(Guid hostId, string username)
    {
        JwtOptions jwt = options.Value;
        DateTimeOffset issuedAt = timeProvider.GetUtcNow();
        DateTimeOffset expiresAt = issuedAt.AddMinutes(jwt.AccessTokenMinutes);

        SigningCredentials credentials = new(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        SecurityTokenDescriptor descriptor = new()
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = issuedAt.UtcDateTime,
            NotBefore = issuedAt.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = credentials,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = hostId.ToString(),
                [JwtRegisteredClaimNames.Name] = username,
                [ClaimTypes.Role] = "Host"
            }
        };

        string token = new JsonWebTokenHandler().CreateToken(descriptor);

        return new AccessTokenResult(token, expiresAt);
    }
}
