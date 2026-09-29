namespace Kahoot.Domain.Entities;

public sealed class RefreshToken
{
    // Primary Identity - Unique identifier for the refresh token record
    public Guid Id { get; set; }

    // User Identity - References the user account authorized by this refresh token
    public Guid UserId { get; set; }

    // Token Family Tracking (AUTH-ROT-001) - Shared GUID grouping consecutive rotated tokens across an ongoing login session
    public Guid TokenFamilyId { get; set; }

    // Session Longevity Boundary - Creation timestamp of the root token in the family to enforce absolute session lifespan
    public DateTimeOffset FamilyCreatedAt { get; set; }

    // Cryptographic Token Digest (AUTH-SEC-001) - SHA-256 hash of the high-entropy random refresh token secret
    public required byte[] TokenHash { get; set; }

    // Temporal Auditability - Timestamp when this specific refresh token in the family was generated
    public DateTimeOffset CreatedAt { get; set; }

    // Absolute Expiration (AUTH-EXP-001) - Sliding expiration timestamp after which the token cannot be exchanged
    public DateTimeOffset ExpiresAt { get; set; }

    // Rotation Invalidation (AUTH-ROT-002) - Timestamp when token was successfully exchanged; reuse triggers immediate family revocation
    public DateTimeOffset? RotatedAt { get; set; }

    // Explicit Revocation (AUTH-REV-001) - Timestamp when token was invalidated by logout, administrative action, or reuse detection
    public DateTimeOffset? RevokedAt { get; set; }
}
