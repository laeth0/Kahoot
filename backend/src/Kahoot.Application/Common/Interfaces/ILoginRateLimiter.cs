namespace Kahoot.Application.Common.Interfaces;

public interface ILoginRateLimiter
{
    bool IsIpRateLimited(string ipAddress);

    TimeSpan GetUsernameBackoffDelay(string normalizedUsername);

    void RecordFailedAttempt(string normalizedUsername);

    void ResetFailedAttempts(string normalizedUsername);
}
