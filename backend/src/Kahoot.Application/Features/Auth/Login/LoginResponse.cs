namespace Kahoot.Application.Features.Auth.Login;

public sealed record LoginResponse(
    Guid AccountId,
    string Username,
    string AccountKind,
    string AccessToken,
    int ExpiresIn = 900);
