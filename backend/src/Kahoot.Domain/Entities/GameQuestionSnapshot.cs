namespace Kahoot.Domain.Entities;

public sealed class GameQuestionSnapshot
{
    // Primary Identity - Unique identifier for the question snapshot
    public Guid Id { get; set; }

    // Multi-Tenant Isolation (TENANT-001) - Scopes question snapshot ownership to the authenticated host account
    public Guid HostAccountId { get; set; }

    // Relational Hierarchy - References the live game session containing this snapshot
    public Guid GameId { get; set; }

    // Sequential Run Index - Zero-based execution order index of the question within the game session
    public int OrderIndex { get; set; }

    // Immutable Question Prompt (GAME-SNAP-001) - Deep copy of question text frozen at game creation under RepeatableRead
    public required string Text { get; set; }

    // Image Reference - Preserved reference to attached image preventing orphan purging while game history exists
    public Guid? ImageId { get; set; }

    // Materialized Media URL - Pre-resolved image storage path enabling high-performance client rendering
    public string? ImageUrl { get; set; }

    // Question Countdown Timer (GAME-SNAP-002) - Immutable duration in seconds for the active answering window
    public int DurationSeconds { get; set; }

    // Base Scoring Value - Base points awarded for a correct answer before speed decay calculation
    public int BasePoints { get; set; }

    // Gameplay Timing (PLAY-TIME-001) - Authoritative server timestamp when question was opened for submissions
    public DateTimeOffset? StartedAt { get; set; }

    // Gameplay Timing (PLAY-TIME-002) - Authoritative deadline when question submissions close
    public DateTimeOffset? EndsAt { get; set; }

    // Baseline Eligibility Metric - Count of active non-removed participants present when question started
    public int InitialEligibleParticipantCount { get; set; }

    // Dynamic Auto-Close Metric (GAME-AUTO-002) - Adjusted eligibility count decremented upon participant ejection to trigger auto-close
    public int EffectiveEligibleParticipantCount { get; set; }

    // Submission Counter (GAME-AUTO-001) - Total valid answers received; triggers auto-close when matching EffectiveEligibleParticipantCount
    public int AcceptedAnswerCount { get; set; }

    // Statistical Materialization Timestamp - Marks when question answers were aggregated and scored
    public DateTimeOffset? ResultsMaterializedAt { get; set; }
}
