namespace Kahoot.Domain.Entities;

public sealed class AnswerSubmission
{
    public Guid Id { get; set; }

    public Guid HostAccountId { get; set; }

    public Guid GameId { get; set; }

    public Guid GameQuestionId { get; set; }

    public Guid ParticipantId { get; set; }

    public DateTimeOffset SubmittedAt { get; set; }

    public int ResponseTimeMs { get; set; }

    public bool IsCorrect { get; set; }

    public int PointsAwarded { get; set; }
}
