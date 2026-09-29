namespace Kahoot.Application.Common.Interfaces;

// Multi-Tier Answer Submission Rate Limiter - Enforces per-socket rolling burst limits and participant-scoped question attempt limits.
public interface IAnswerRateLimiter
{
    // Multi-Tier Rate Limit Evaluation (PLAY-RATE-001, PLAY-RATE-002) - Rejects attempts exceeding 5 per 3s on socket or 10 per question on participant.
    Task<bool> IsRateLimitedAsync(
        string connectionId,
        Guid gameId,
        Guid participantId,
        Guid questionId,
        CancellationToken cancellationToken);
}
