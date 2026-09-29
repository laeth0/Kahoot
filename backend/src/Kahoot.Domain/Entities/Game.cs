using Kahoot.Domain.Enums;

namespace Kahoot.Domain.Entities;

public sealed class Game
{
    // Primary Identity - Unique identifier for the live game session
    public Guid Id { get; set; }

    // Multi-Tenant Isolation (TENANT-001) - Scopes live game session strictly to the authenticated host account
    public Guid HostAccountId { get; set; }

    // Quiz Template Provenance - References the source quiz from which immutable question snapshots were generated
    public Guid SourceQuizId { get; set; }

    // Session Title - Display title cloned from source quiz at game creation
    public required string Title { get; set; }

    // Ephemeral Discovery PIN (JOIN-PIN-001) - Unique 6-digit PIN active while game is joinable; cleared upon game completion
    public string? Pin { get; set; }

    // Authoritative State Machine (GAME-STATE-001) - Current state across the 6-phase game lifecycle
    public GameStatus Status { get; set; } = GameStatus.Created;

    // State Transition Fencing (RT-STATE-001) - Monotonically increasing state version broadcast to clients to reject out-of-order frames
    public long StateVersion { get; set; } = 1;

    // Presence Epoch Counter - Incremented whenever participants join, disconnect, or are evicted for lobby synchronization
    public long PresenceVersion { get; set; }

    // Capacity Management (JOIN-CAP-001) - Current reserved seats tracking active players against the 500-seat ceiling
    public int ReservedParticipantCount { get; set; }

    // Monotonic Seat Allocator - Next contiguous seat sequence number assigned to joining participants
    public int NextSeatNumber { get; set; } = 1;

    // Gameplay Progress Pointer - Index of the currently active question snapshot in the sequential quiz run
    public int? CurrentQuestionIndex { get; set; }

    // Distributed Heartbeat Lease (HOST-PRES-001) - Timestamp when host disconnect grace period expires before game auto-aborts
    public DateTimeOffset? HostGraceExpiresAt { get; set; }

    // Administrative Termination Flag (ADMIN-SUSP-001) - Indicates session was aborted due to host account suspension
    public bool IsTerminatedBySuspension { get; set; }

    // Temporal Auditability - Timestamp when the game session was initialized
    public DateTimeOffset CreatedAt { get; set; }

    // Temporal Auditability - Timestamp when the game transitioned to Finished state
    public DateTimeOffset? FinishedAt { get; set; }
}
