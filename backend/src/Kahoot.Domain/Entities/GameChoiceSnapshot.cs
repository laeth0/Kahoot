namespace Kahoot.Domain.Entities;

public sealed class GameChoiceSnapshot
{
    // Primary Identity - Unique identifier for the choice snapshot
    public Guid Id { get; set; }

    // Multi-Tenant Isolation (TENANT-001) - Scopes choice snapshot ownership to the authenticated host account
    public Guid HostAccountId { get; set; }

    // Relational Hierarchy - References the parent question snapshot owning this choice
    public Guid GameQuestionId { get; set; }

    // Immutable Choice Text (GAME-SNAP-001) - Deep copy of choice prompt frozen at game creation
    public required string Text { get; set; }

    // Answer Scoring Rule (SCORE-RULE-001) - Authoritative flag indicating whether selecting this choice awards points
    public bool IsCorrect { get; set; }

    // Sequential Run Index - Zero-based display order index of the choice
    public int OrderIndex { get; set; }

    // Materialized Metric - Aggregated count of participants who selected this choice option
    public int SelectionCount { get; set; }
}
