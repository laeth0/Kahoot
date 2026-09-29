namespace Kahoot.Domain.Entities;

public sealed class ParticipantSessionToken
{
    // Primary Identity - Unique identifier for the participant session token record
    public Guid Id { get; set; }

    // Multi-Tenant Isolation (TENANT-001) - Scopes token ownership to the authenticated host account
    public Guid HostAccountId { get; set; }

    // Relational Hierarchy - References the live game session
    public Guid GameId { get; set; }

    // Participant Association - References the participant authorized by this session token
    public Guid ParticipantId { get; set; }

    // Cryptographic Token Digest (JOIN-AUTH-001) - SHA-256 hash of the high-entropy bearer token issued to the client
    public required byte[] TokenHash { get; set; }

    // Temporal Auditability - Timestamp when session token was generated
    public DateTimeOffset CreatedAt { get; set; }

    // Token Revocation (LOBBY-EVICT-001) - Set when participant is kicked, revoked, or leaves to prevent subsequent hub authentication
    public DateTimeOffset? RevokedAt { get; set; }

    // Session Expiration - Timestamp after which the token is invalid for reconnection
    public DateTimeOffset? ExpiresAt { get; set; }
}
