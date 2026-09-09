using Kahoot.Domain.Common;

namespace Kahoot.Domain.Quizzes;

public sealed class Question : AuditableEntity
{
    public Guid QuizId { get; set; }

    public int OrderIndex { get; set; }

    public string Text { get; set; } = null!;

    public int TimeLimitSeconds { get; set; }

    public int Points { get; set; }

    public Quiz? Quiz { get; set; }

    public ICollection<Choice> Choices { get; set; } = [];
}
