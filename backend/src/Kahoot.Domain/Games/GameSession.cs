using Kahoot.Domain.Common;
using Kahoot.Domain.Hosts;
using Kahoot.Domain.Quizzes;

namespace Kahoot.Domain.Games;

public sealed class GameSession : AuditableEntity
{
    private readonly List<Participant> _participants = [];
    private readonly List<Answer> _answers = [];

    private GameSession()
    {
    }

    private GameSession(Guid quizId, Guid hostId, string pin)
    {
        QuizId = quizId;
        HostId = hostId;
        Pin = pin;
        Status = GameStatus.Created;
    }

    public Guid QuizId { get; private set; }

    public Guid HostId { get; private set; }

    public string Pin { get; private set; } = null!;

    public GameStatus Status { get; private set; }

    public Guid? CurrentQuestionId { get; private set; }

    public int? CurrentQuestionIndex { get; private set; }

    public DateTime? CurrentQuestionStartedAtUtc { get; private set; }

    public DateTime? CurrentQuestionEndsAtUtc { get; private set; }

    public DateTime? StartedAtUtc { get; private set; }

    public DateTime? FinishedAtUtc { get; private set; }

    public Quiz? Quiz { get; private set; }

    public Host? Host { get; private set; }

    public IReadOnlyCollection<Participant> Participants => _participants.AsReadOnly();

    public IReadOnlyCollection<Answer> Answers => _answers.AsReadOnly();

    public static GameSession Create(Guid quizId, Guid hostId, string pin)
    {
        if (string.IsNullOrWhiteSpace(pin))
        {
            throw new ArgumentException("PIN is required.", nameof(pin));
        }

        return new GameSession(quizId, hostId, pin);
    }

    public void OpenLobby() => TransitionTo(GameStatus.Lobby);

    public void StartQuestion(Guid questionId, int questionIndex, int timeLimitSeconds, DateTime nowUtc)
    {
        if (questionIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(questionIndex), questionIndex, "Question index cannot be negative.");
        }

        if (timeLimitSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(timeLimitSeconds), timeLimitSeconds, "Time limit must be positive.");
        }

        TransitionTo(GameStatus.QuestionActive);

        CurrentQuestionId = questionId;
        CurrentQuestionIndex = questionIndex;
        CurrentQuestionStartedAtUtc = nowUtc;
        CurrentQuestionEndsAtUtc = nowUtc.AddSeconds(timeLimitSeconds);
        StartedAtUtc ??= nowUtc;
    }

    public void EndQuestion() => TransitionTo(GameStatus.QuestionResults);

    public void ShowLeaderboard() => TransitionTo(GameStatus.Leaderboard);

    public void Finish(DateTime nowUtc)
    {
        TransitionTo(GameStatus.Finished);
        FinishedAtUtc = nowUtc;
    }

    public bool IsAcceptingAnswersFor(Guid questionId, DateTime nowUtc) =>
        Status == GameStatus.QuestionActive
        && CurrentQuestionId == questionId
        && CurrentQuestionEndsAtUtc is { } endsAtUtc
        && nowUtc <= endsAtUtc;

    private void TransitionTo(GameStatus target)
    {
        if (!GameStatusTransitions.IsAllowed(Status, target))
        {
            throw new InvalidGameStatusTransitionException(Status, target);
        }

        Status = target;
    }
}
