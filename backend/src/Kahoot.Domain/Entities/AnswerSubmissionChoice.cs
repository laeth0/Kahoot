namespace Kahoot.Domain.Entities;

public sealed class AnswerSubmissionChoice
{
    public Guid AnswerSubmissionId { get; set; }

    public Guid HostAccountId { get; set; }

    public Guid GameQuestionId { get; set; }

    public Guid GameChoiceId { get; set; }
}
