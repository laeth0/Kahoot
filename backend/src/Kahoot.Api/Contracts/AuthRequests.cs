namespace Kahoot.Api.Contracts;

public sealed record LoginRequest(string Username, string Password);

public sealed record RefreshRequest(string? RefreshToken = null);

public sealed record LogoutRequest(string? RefreshToken = null);

public sealed record HostAuthResponse(
    Guid HostId,
    string Username,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt);
