namespace Kahoot.Domain.Games;

public sealed class GameQuestionSnapshot
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid GameSessionId { get; set; }

    public Guid? SourceQuestionId { get; set; }

    public int OrderIndex { get; set; }

    public string Text { get; set; } = null!;

    public string? ImageUrl { get; set; }

    public int TimeLimitSeconds { get; set; }

    public int Points { get; set; }

    public GameSession? GameSession { get; set; }

    public ICollection<GameChoiceSnapshot> Choices { get; set; } = [];

    public ICollection<Answer> Answers { get; set; } = [];
}
