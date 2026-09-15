namespace Kahoot.Domain.Games;

public sealed class GameChoiceSnapshot
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid QuestionSnapshotId { get; set; }

    public Guid? SourceChoiceId { get; set; }

    public int OrderIndex { get; set; }

    public string Text { get; set; } = string.Empty;

    public bool IsCorrect { get; set; }

    public GameQuestionSnapshot? QuestionSnapshot { get; set; }

    public ICollection<AnswerSelectedChoice> SelectedChoices { get; set; } = [];
}
