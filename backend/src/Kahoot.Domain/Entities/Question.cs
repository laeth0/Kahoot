namespace Kahoot.Domain.Entities;

public sealed class Question
{
    // Primary Identity - Unique identifier for the question
    public Guid Id { get; set; }

    // Multi-Tenant Isolation (TENANT-001) - Scopes question ownership to the authenticated host account
    public Guid HostAccountId { get; set; }

    // Relational Hierarchy - References the parent quiz containing this question
    public Guid QuizId { get; set; }

    // Question Prompt (QUIZ-QUEST-001) - Plain-text prompt bounded between 1 and 500 characters
    public required string Text { get; set; }

    // Single-Question Image Attachment (QUIZ-SEC-001, IMG-ATT-001) - Optional reference to an image owned by the host
    public Guid? ImageId { get; set; }

    // Relational Navigation - Strongly-typed entity navigation to the attached QuestionImage
    public QuestionImage? Image { get; set; }

    // Question Timer (QUIZ-QUEST-002) - Duration of the countdown timer bounded between 5 and 300 seconds
    public int DurationSeconds { get; set; }

    // Scoring Weight (QUIZ-QUEST-003) - Non-negative base points awarded for correct answers
    public int BasePoints { get; set; }

    // Sequential Permutation Index (QUIZ-REORDER-001) - Zero-based contiguous index unique per (QuizId, OrderIndex)
    public int OrderIndex { get; set; }
}
