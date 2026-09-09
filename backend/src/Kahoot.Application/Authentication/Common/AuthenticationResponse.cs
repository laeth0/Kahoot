namespace Kahoot.Application.Authentication.Common;

public sealed record AuthenticationResponse(
    Guid HostId,
    string Username,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);
