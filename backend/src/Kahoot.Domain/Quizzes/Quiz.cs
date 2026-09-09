using Kahoot.Domain.Common;
using Kahoot.Domain.Hosts;

namespace Kahoot.Domain.Quizzes;

public sealed class Quiz : AuditableEntity
{
    private readonly List<Question> _questions = [];

    private Quiz()
    {
    }

    private Quiz(Guid hostId, string title, string? description)
    {
        HostId = hostId;
        Title = title;
        Description = description;
    }

    public Guid HostId { get; private set; }

    public string Title { get; private set; } = null!;

    public string? Description { get; private set; }

    public Host? Host { get; private set; }

    public IReadOnlyCollection<Question> Questions => _questions.AsReadOnly();

    public static Quiz Create(Guid hostId, string title, string? description)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title is required.", nameof(title));
        }

        return new Quiz(hostId, title.Trim(), NormalizeOptional(description));
    }

    public void UpdateDetails(string title, string? description)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title is required.", nameof(title));
        }

        Title = title.Trim();
        Description = NormalizeOptional(description);
    }

    public Question AddQuestion(string text, int timeLimitSeconds, int points)
    {
        var question = Question.Create(Id, _questions.Count, text, timeLimitSeconds, points);
        _questions.Add(question);
        return question;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
