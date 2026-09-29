namespace Kahoot.Domain.Entities;

public sealed class AnswerSubmission
{
    // Primary Identity - Unique identifier for the answer submission record
    public Guid Id { get; set; }

    // Multi-Tenant Isolation (TENANT-001) - Scopes submission ownership to the authenticated host account
    public Guid HostAccountId { get; set; }

    // Relational Hierarchy - References the live game session
    public Guid GameId { get; set; }

    // Relational Hierarchy - References the specific question snapshot answered
    public Guid GameQuestionId { get; set; }

    // Participant Attribution - References the participant submitting this answer
    public Guid ParticipantId { get; set; }

    // Temporal Auditability - Server-side timestamp when answer was received and validated
    public DateTimeOffset SubmittedAt { get; set; }

    // Response Latency Metric (SCORE-TIME-001) - Server-calculated elapsed time in milliseconds between Question.StartedAt and SubmittedAt
    public int ResponseTimeMs { get; set; }

    // Answer Correctness Evaluation (SCORE-EVAL-001) - Evaluated boolean indicating full accuracy of submitted choice(s)
    public bool IsCorrect { get; set; }

    // Speed-Decay Score (SCORE-FORMULA-001) - Points awarded using formula: BasePoints * (1 - (ResponseTimeMs / DurationMs) * 0.5)
    public int PointsAwarded { get; set; }
}
