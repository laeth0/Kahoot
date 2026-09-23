namespace Kahoot.Domain.Entities;

public sealed class Choice
{
    public Guid Id { get; set; }

    public Guid HostAccountId { get; set; }

    public Guid QuestionId { get; set; }

    public required string Text { get; set; }

    public bool IsCorrect { get; set; }

    public int OrderIndex { get; set; }
}
