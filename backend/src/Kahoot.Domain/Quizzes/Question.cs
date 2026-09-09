using Kahoot.Domain.Common;

namespace Kahoot.Domain.Quizzes;

public sealed class Question : AuditableEntity
{
    public const int MinTimeLimitSeconds = 5;
    public const int MaxTimeLimitSeconds = 300;
    public const int MinChoices = 2;
    public const int MaxChoices = 6;

    private readonly List<Choice> _choices = [];

    private Question()
    {
    }

    private Question(Guid quizId, int orderIndex, string text, int timeLimitSeconds, int points)
    {
        QuizId = quizId;
        OrderIndex = orderIndex;
        Text = text;
        TimeLimitSeconds = timeLimitSeconds;
        Points = points;
    }

    public Guid QuizId { get; private set; }

    public int OrderIndex { get; private set; }

    public string Text { get; private set; } = null!;

    public int TimeLimitSeconds { get; private set; }

    public int Points { get; private set; }

    public Quiz? Quiz { get; private set; }

    public IReadOnlyCollection<Choice> Choices => _choices.AsReadOnly();

    public static Question Create(Guid quizId, int orderIndex, string text, int timeLimitSeconds, int points)
    {
        EnsureValidOrderIndex(orderIndex);
        EnsureValidText(text);
        EnsureValidTimeLimit(timeLimitSeconds);
        EnsureValidPoints(points);

        return new Question(quizId, orderIndex, text.Trim(), timeLimitSeconds, points);
    }

    public void UpdateDetails(string text, int timeLimitSeconds, int points)
    {
        EnsureValidText(text);
        EnsureValidTimeLimit(timeLimitSeconds);
        EnsureValidPoints(points);

        Text = text.Trim();
        TimeLimitSeconds = timeLimitSeconds;
        Points = points;
    }

    public Choice AddChoice(string text, bool isCorrect)
    {
        var choice = Choice.Create(Id, _choices.Count, text, isCorrect);
        _choices.Add(choice);
        return choice;
    }

    public bool HasValidChoiceCount() => _choices.Count is >= MinChoices and <= MaxChoices;

    public bool HasExactlyOneCorrectChoice() => _choices.Count(choice => choice.IsCorrect) == 1;

    private static void EnsureValidOrderIndex(int orderIndex)
    {
        if (orderIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(orderIndex), orderIndex, "Order index cannot be negative.");
        }
    }

    private static void EnsureValidText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Question text is required.", nameof(text));
        }
    }

    private static void EnsureValidTimeLimit(int timeLimitSeconds)
    {
        if (timeLimitSeconds is < MinTimeLimitSeconds or > MaxTimeLimitSeconds)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeLimitSeconds),
                timeLimitSeconds,
                $"Time limit must be between {MinTimeLimitSeconds} and {MaxTimeLimitSeconds} seconds.");
        }
    }

    private static void EnsureValidPoints(int points)
    {
        if (points < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(points), points, "Points cannot be negative.");
        }
    }
}
