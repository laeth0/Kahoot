namespace Kahoot.Application.Common.Exceptions;

// Concurrency Saturation Barrier (AUTH-HASH-001) - Thrown when global Argon2id hashing capacity (16 active, 50 queued) is exhausted
public sealed class PasswordHashingRateLimitedException : Exception
{
    public PasswordHashingRateLimitedException(string message = "Password hashing concurrency limit exceeded.")
        : base(message)
    {
    }
}
