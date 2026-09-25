namespace Kahoot.Application.Features.Auth.Refresh;

public sealed record RefreshResponse(
    Guid AccountId,
    string Username,
    string AccountKind,
    string AccessToken,
    int ExpiresIn = 900);
