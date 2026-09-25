namespace Kahoot.Application.Features.Auth.Login;

public sealed record LoginResult(
    LoginResponse Response,
    string RawRefreshToken);
