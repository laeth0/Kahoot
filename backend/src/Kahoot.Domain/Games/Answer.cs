using Kahoot.Domain.Quizzes;

namespace Kahoot.Domain.Games;

public sealed class Answer
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid GameSessionId { get; set; }

    public Guid QuestionId { get; set; }

    public Guid ParticipantId { get; set; }

    public Guid SelectedChoiceId { get; set; }

    public bool IsCorrect { get; set; }

    public int PointsAwarded { get; set; }

    public int ResponseTimeMs { get; set; }

    public DateTime SubmittedAt { get; set; }

    public GameSession? GameSession { get; set; }

    public Question? Question { get; set; }

    public Participant? Participant { get; set; }

    public Choice? SelectedChoice { get; set; }
}
