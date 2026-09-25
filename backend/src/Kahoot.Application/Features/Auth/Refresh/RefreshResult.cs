namespace Kahoot.Application.Features.Auth.Refresh;

public sealed record RefreshResult(
    RefreshResponse Response,
    string RawRefreshToken);
