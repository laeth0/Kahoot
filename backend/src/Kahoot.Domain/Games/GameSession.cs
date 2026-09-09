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

    public Result OpenLobby() => TransitionTo(GameStatus.Lobby);

    public Result StartQuestion(Guid questionId, int questionIndex, int timeLimitSeconds, DateTime nowUtc)
    {
        if (questionIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(questionIndex), questionIndex, "Question index cannot be negative.");
        }

        if (timeLimitSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(timeLimitSeconds), timeLimitSeconds, "Time limit must be positive.");
        }

        var transition = TransitionTo(GameStatus.QuestionActive);
        if (transition.IsFailure)
        {
            return transition;
        }

        CurrentQuestionId = questionId;
        CurrentQuestionIndex = questionIndex;
        CurrentQuestionStartedAtUtc = nowUtc;
        CurrentQuestionEndsAtUtc = nowUtc.AddSeconds(timeLimitSeconds);
        StartedAtUtc ??= nowUtc;

        return Result.Success();
    }

    public Result EndQuestion() => TransitionTo(GameStatus.QuestionResults);

    public Result ShowLeaderboard() => TransitionTo(GameStatus.Leaderboard);

    public Result Finish(DateTime nowUtc)
    {
        var transition = TransitionTo(GameStatus.Finished);
        if (transition.IsFailure)
        {
            return transition;
        }

        FinishedAtUtc = nowUtc;

        return Result.Success();
    }

    public bool IsAcceptingAnswersFor(Guid questionId, DateTime nowUtc) =>
        Status == GameStatus.QuestionActive
        && CurrentQuestionId == questionId
        && CurrentQuestionEndsAtUtc is { } endsAtUtc
        && nowUtc <= endsAtUtc;

    private Result TransitionTo(GameStatus target)
    {
        if (!GameStatusTransitions.IsAllowed(Status, target))
        {
            return Result.Failure(GameErrors.InvalidStatusTransition(Status, target));
        }

        Status = target;

        return Result.Success();
    }
}
