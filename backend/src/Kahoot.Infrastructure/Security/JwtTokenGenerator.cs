using System.Security.Claims;
using Kahoot.Application.Common;
using Kahoot.Application.Common.Interfaces;
using Kahoot.Domain.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Kahoot.Infrastructure.Security;

public sealed class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtOptions _options;
    private readonly byte[] _signingKeyBytes;
    private readonly TimeProvider _timeProvider;

    public JwtTokenGenerator(IOptions<JwtOptions> options, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _options = options.Value;
        _signingKeyBytes = Convert.FromBase64String(_options.SigningKey);
        _timeProvider = timeProvider;
    }

    public AccessTokenResult GenerateAccessToken(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        DateTimeOffset now = _timeProvider.GetUtcNow();
        // Bounded Lifetime - Short 15-minute validity window minimizes exposure of bearer tokens
        DateTimeOffset expiresAt = now.AddMinutes(_options.AccessTokenMinutes);

        SymmetricSecurityKey securityKey = new SymmetricSecurityKey(_signingKeyBytes);
        SigningCredentials credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        // Claims Topology & Security Versioning - Injects sub/accountId boundary, role, and token_security_version for immediate cluster-wide revocation
        Dictionary<string, object> claims = new Dictionary<string, object>
        {
            [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
            ["accountId"] = user.Id.ToString(),
            [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
            [JwtRegisteredClaimNames.UniqueName] = user.DisplayUsername,
            ["role"] = user.Role.ToString(),
            ["token_security_version"] = user.TokenSecurityVersion,
            ["tokenSecurityVersion"] = user.TokenSecurityVersion
        };

        // High-Performance Token Issuance - Uses memory-optimized JsonWebTokenHandler (~30% faster than legacy JwtSecurityTokenHandler)
        JsonWebTokenHandler handler = new JsonWebTokenHandler();
        SecurityTokenDescriptor descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = credentials,
            Claims = claims
        };

        string token = handler.CreateToken(descriptor);
        int expiresInSeconds = (int)(_options.AccessTokenMinutes * 60);

        return new AccessTokenResult(token, expiresAt, expiresInSeconds);
    }
}

