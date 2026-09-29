namespace Kahoot.Domain.Entities;

public sealed class Quiz : IAuditableEntity
{
    // Primary Identity - Unique identifier for the quiz
    public Guid Id { get; set; }

    // Multi-Tenant Isolation (TENANT-001) - Scopes quiz ownership to the authenticated host account
    public Guid HostAccountId { get; set; }

    // Display Title (QUIZ-TITLE-001) - Plain-text title bounded between 1 and 200 characters
    public required string Title { get; set; }

    // Optional Metadata - Descriptive summary bounded to a maximum of 1000 characters
    public string? Description { get; set; }

    // Monotonic Revision Counter (QUIZ-REV-001) - Incremented on every quiz/question edit for cache invalidation and OCC
    public long Revision { get; set; } = 1;

    // Temporal Auditability - Timestamp when the quiz was created
    public DateTimeOffset CreatedAt { get; set; }

    // Temporal Auditability - Timestamp when the quiz was last modified
    public DateTimeOffset UpdatedAt { get; set; }

    // Actor Auditability - Identifier of the host account that created the quiz
    public Guid? CreatedBy { get; set; }

    // Actor Auditability - Identifier of the host account that last updated the quiz
    public Guid? UpdatedBy { get; set; }
}
