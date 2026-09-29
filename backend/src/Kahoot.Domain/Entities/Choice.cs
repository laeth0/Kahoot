namespace Kahoot.Domain.Entities;

public sealed class Choice
{
    // Primary Identity - Unique identifier for the answer choice
    public Guid Id { get; set; }

    // Multi-Tenant Isolation (TENANT-001) - Scopes choice ownership to the authenticated host account
    public Guid HostAccountId { get; set; }

    // Relational Hierarchy - References the parent question owning this choice
    public Guid QuestionId { get; set; }

    // Choice Text (QUIZ-QUEST-004) - Plain-text choice string bounded between 1 and 300 characters
    public required string Text { get; set; }

    // Answer Scoring Rule (QUIZ-QUEST-004) - Indicates whether selecting this choice counts as a correct answer
    public bool IsCorrect { get; set; }

    // Sequential Display Index - Zero-based contiguous ordering index unique per (QuestionId, OrderIndex)
    public int OrderIndex { get; set; }
}
