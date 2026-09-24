namespace Kahoot.Application.Common.Exceptions;

/// <summary>
/// Thrown when the global password hashing concurrency floor (16 active, 50 queued) is exceeded.
/// </summary>
public sealed class PasswordHashingRateLimitedException : Exception
{
    public PasswordHashingRateLimitedException(string message = "Password hashing concurrency limit exceeded.")
        : base(message)
    {
    }
}
