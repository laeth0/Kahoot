namespace Kahoot.Application.Common.Interfaces;

public interface ILobbyJoinRateLimiter
{
    Task<bool> IsRateLimitedAsync(string ipAddress, CancellationToken cancellationToken);
}
