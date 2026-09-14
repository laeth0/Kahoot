namespace Kahoot.Domain.Games;

public sealed class AnswerSelectedChoice
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid AnswerId { get; set; }

    public Guid SelectedChoiceId { get; set; }

    public Answer? Answer { get; set; }

    public GameChoiceSnapshot? SelectedChoice { get; set; }
}
