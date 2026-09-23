namespace Kahoot.Application.Common.Authentication;

public sealed record TokenPair(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    DateTimeOffset RefreshTokenExpiresAt);
