namespace Kahoot.Domain.Entities;

public sealed class Participant
{
    // Primary Identity - Unique identifier for the game participant
    public Guid Id { get; set; }

    // Multi-Tenant Isolation (TENANT-001) - Scopes participant ownership to the authenticated host account
    public Guid HostAccountId { get; set; }

    // Relational Hierarchy - References the live game session this participant joined
    public Guid GameId { get; set; }

    // Display Nickname (JOIN-NICK-001) - Case-preserving nickname chosen by player (1-30 chars)
    public required string DisplayNickname { get; set; }

    // Canonical Unique Reservation (JOIN-NICK-002) - Unicode NFKC normalized and uppercase folded; acts as permanent tombstone to prevent duplicate name reuse
    public required string NormalizedNickname { get; set; }

    // Fixed Seat Identifier (JOIN-CAP-001) - Allocated seat sequence number (1 to 500) for capacity tracking
    public int SeatNumber { get; set; }

    // Join Idempotency Key (JOIN-IDEMP-001) - SHA-256 digest of client-provided UUID enabling idempotent join recovery
    public required byte[] JoinOperationIdHash { get; set; }

    // Idempotent Recovery Window (JOIN-IDEMP-002) - Grace period (2 minutes) allowing client to retrieve lost token on network failure
    public DateTimeOffset JoinRecoveryExpiresAt { get; set; }

    // Ejection Tombstone (LOBBY-EVICT-001) - Flag indicating participant was removed by host or left the game
    public bool IsRemoved { get; set; }

    // Temporal Auditability - Timestamp when participant was removed from the game session
    public DateTimeOffset? RemovedAt { get; set; }

    // Scoring Accumulator (SCORE-CALC-001) - Cumulative points earned across answered questions
    public long TotalScore { get; set; }

    // Leaderboard Standing - Materialized rank position computed during Leaderboard phase
    public int? Rank { get; set; }

    // Connection Generation Fencing (RECON-FENCE-001) - Incremented on every reconnect to instantly reject stale duplicate socket connections
    public long ConnectionGeneration { get; set; }

    // Temporal Auditability - Timestamp when participant completed lobby joining
    public DateTimeOffset CreatedAt { get; set; }
}
