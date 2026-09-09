using Kahoot.Domain.Common;

namespace Kahoot.Domain.Quizzes;

public sealed class Choice : AuditableEntity
{
    public Guid QuestionId { get; set; }

    public int OrderIndex { get; set; }

    public string Text { get; set; } = null!;

    public bool IsCorrect { get; set; }

    public Question? Question { get; set; }
}
