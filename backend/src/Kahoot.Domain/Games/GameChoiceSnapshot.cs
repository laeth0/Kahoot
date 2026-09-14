namespace Kahoot.Domain.Games;

public sealed class GameChoiceSnapshot
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid QuestionSnapshotId { get; set; }

    public Guid? SourceChoiceId { get; set; }

    public int OrderIndex { get; set; }

    public string? Text { get; set; }

    public string? ImageUrl { get; set; }

    public bool IsCorrect { get; set; }

    public GameQuestionSnapshot? QuestionSnapshot { get; set; }

    public ICollection<Answer> Answers { get; set; } = [];
}
