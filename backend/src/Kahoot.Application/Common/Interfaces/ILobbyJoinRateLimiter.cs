namespace Kahoot.Application.Common.Interfaces;

public interface ILobbyJoinRateLimiter
{
    // Token Bucket Rate Limiter (JOIN-RATE-001) - Enforces 1200 capacity token bucket refilled at 60 tokens/sec per IP address
    Task<bool> IsRateLimitedAsync(string ipAddress, CancellationToken cancellationToken);
}
