namespace Kahoot.Domain.Entities;

public sealed class GameCommandIdempotency
{
    // Relational Hierarchy - References the game session target of the idempotent command
    public Guid GameId { get; set; }

    // Multi-Tenant Isolation (TENANT-001) - Scopes idempotency log to the authenticated host account
    public Guid HostAccountId { get; set; }

    // Idempotent Command Key (GAME-IDEMP-001) - Client-provided unique UUID identifying this specific command invocation
    public Guid CommandId { get; set; }

    // Command Type Descriptor - Action identifier (e.g., StartGame, AdvanceQuestion, EndGame)
    public required string CommandName { get; set; }

    // Cryptographic Payload Hash (GAME-IDEMP-002) - SHA-256 hash of request parameters to detect conflicting payload re-execution
    public required byte[] RequestHash { get; set; }

    // State Transition Anchor - Authoritative Game.StateVersion resulting from successful execution of this command
    public long ResultStateVersion { get; set; }

    // Cached Result Payload - Serialized JSON response replayed on duplicate command submission
    public required string ResponsePayload { get; set; }

    // Temporal Auditability - Timestamp when the command was originally executed and recorded
    public DateTimeOffset CreatedAt { get; set; }
}
