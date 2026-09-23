namespace Kahoot.Domain.Entities;

public sealed class GameChoiceSnapshot
{
    public Guid Id { get; set; }

    public Guid HostAccountId { get; set; }

    public Guid GameQuestionId { get; set; }

    public required string Text { get; set; }

    public bool IsCorrect { get; set; }

    public int OrderIndex { get; set; }

    public int SelectionCount { get; set; }
}
