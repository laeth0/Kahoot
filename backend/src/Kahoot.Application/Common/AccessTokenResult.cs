namespace Kahoot.Application.Common;

/// <summary>
/// The result of generating a JWT access token, carrying the token string
/// alongside its expiration metadata so callers never have to duplicate
/// or guess the lifetime configured in JwtOptions.
/// </summary>
public sealed record AccessTokenResult(
    string Token,
    DateTimeOffset ExpiresAt,
    int ExpiresInSeconds);
