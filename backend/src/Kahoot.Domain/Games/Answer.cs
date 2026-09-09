using Kahoot.Domain.Quizzes;

namespace Kahoot.Domain.Games;

public sealed class Answer
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    private Answer()
    {
    }

    private Answer(
        Guid gameSessionId,
        Guid questionId,
        Guid participantId,
        Guid selectedChoiceId,
        bool isCorrect,
        int pointsAwarded,
        int responseTimeMs,
        DateTime submittedAtUtc)
    {
        GameSessionId = gameSessionId;
        QuestionId = questionId;
        ParticipantId = participantId;
        SelectedChoiceId = selectedChoiceId;
        IsCorrect = isCorrect;
        PointsAwarded = pointsAwarded;
        ResponseTimeMs = responseTimeMs;
        SubmittedAtUtc = submittedAtUtc;
    }

    public Guid GameSessionId { get; private set; }

    public Guid QuestionId { get; private set; }

    public Guid ParticipantId { get; private set; }

    public Guid SelectedChoiceId { get; private set; }

    public bool IsCorrect { get; private set; }

    public int PointsAwarded { get; private set; }

    public int ResponseTimeMs { get; private set; }

    public DateTime SubmittedAtUtc { get; private set; }

    public GameSession? GameSession { get; private set; }

    public Question? Question { get; private set; }

    public Participant? Participant { get; private set; }

    public Choice? SelectedChoice { get; private set; }

    public static Answer Create(
        Guid gameSessionId,
        Guid questionId,
        Guid participantId,
        Guid selectedChoiceId,
        bool isCorrect,
        int pointsAwarded,
        int responseTimeMs,
        DateTime submittedAtUtc)
    {
        if (pointsAwarded < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pointsAwarded), pointsAwarded, "Awarded points cannot be negative.");
        }

        if (responseTimeMs < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(responseTimeMs), responseTimeMs, "Response time cannot be negative.");
        }

        return new Answer(
            gameSessionId,
            questionId,
            participantId,
            selectedChoiceId,
            isCorrect,
            pointsAwarded,
            responseTimeMs,
            submittedAtUtc);
    }
}
