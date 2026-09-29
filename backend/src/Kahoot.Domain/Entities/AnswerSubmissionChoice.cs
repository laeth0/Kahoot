namespace Kahoot.Domain.Entities;

public sealed class AnswerSubmissionChoice
{
    // Relational Hierarchy - References the parent answer submission record
    public Guid AnswerSubmissionId { get; set; }

    // Multi-Tenant Isolation (TENANT-001) - Scopes junction record to the authenticated host account
    public Guid HostAccountId { get; set; }

    // Relational Hierarchy - References the question snapshot answered
    public Guid GameQuestionId { get; set; }

    // Option Selection Attribution - References the specific game choice snapshot selected by the participant
    public Guid GameChoiceId { get; set; }
}
