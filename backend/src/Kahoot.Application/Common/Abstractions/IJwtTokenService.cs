namespace Kahoot.Application.Common.Abstractions;

public interface IJwtTokenService
{
    AccessTokenResult CreateAccessToken(Guid hostId, string username);
}

public sealed record AccessTokenResult(string Token, DateTimeOffset ExpiresAt);
