namespace Kahoot.Domain.Entities;

public sealed class Question
{
    public Guid Id { get; set; }

    public Guid HostAccountId { get; set; }

    public Guid QuizId { get; set; }

    public required string Text { get; set; }

    public Guid? MediaItemId { get; set; }

    public int DurationSeconds { get; set; }

    public int BasePoints { get; set; }

    public int OrderIndex { get; set; }
}
