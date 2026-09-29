namespace Kahoot.Application.Common.Interfaces;

public interface ILoginRateLimiter
{
    // IP Throttle Barrier (AUTH-RATE-001) - Evaluates fixed window rate limit per client IP address
    bool IsIpRateLimited(string ipAddress);

    // Progressive Backoff Delay (AUTH-RATE-002) - Computes exponential cooldown delay based on consecutive failed attempts
    TimeSpan GetUsernameBackoffDelay(string normalizedUsername);

    // Failure Metric Accumulator - Increments failed login count for normalized username
    void RecordFailedAttempt(string normalizedUsername);

    // Failure Counter Reset - Clears failed attempts counter upon successful authentication
    void ResetFailedAttempts(string normalizedUsername);
}
