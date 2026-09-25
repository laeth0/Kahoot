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

        var now = _timeProvider.GetUtcNow();
        var expiresAt = now.AddMinutes(_options.AccessTokenMinutes);

        var securityKey = new SymmetricSecurityKey(_signingKeyBytes);
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var claims = new Dictionary<string, object>
        {
            [JwtRegisteredClaimNames.Sub] = user.Id.ToString(),
            ["accountId"] = user.Id.ToString(),
            [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
            [JwtRegisteredClaimNames.UniqueName] = user.DisplayUsername,
            ["role"] = user.Role.ToString(),
            ["token_security_version"] = user.TokenSecurityVersion,
            ["tokenSecurityVersion"] = user.TokenSecurityVersion
        };

        var handler = new JsonWebTokenHandler();
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = credentials,
            Claims = claims
        };

        var token = handler.CreateToken(descriptor);
        var expiresInSeconds = (int)(_options.AccessTokenMinutes * 60);

        return new AccessTokenResult(token, expiresAt, expiresInSeconds);
    }
}

