using Kahoot.Domain.Enums;

namespace Kahoot.Domain.Entities;

public sealed class User : IAuditableEntity
{
    // Primary Identity - Unique identifier for the user account
    public Guid Id { get; set; }

    // Case-Preserving Display - Original username representation shown in user interfaces
    public required string DisplayUsername { get; set; }

    // Canonical Lookup Key - Unicode NFKC normalized and uppercase folded for case-insensitive unique lookup
    public required string NormalizedUsername { get; set; }

    // Cryptographic Credential Digest - Argon2id secure password hash
    public required string PasswordHash { get; set; }

    // Role-Based Access Control (RBAC-001) - Authoritative role partition (Host vs SystemAdmin)
    public UserRole Role { get; set; } = UserRole.Host;

    // Account Lifecycle State - Administrative status governing authentication and access privileges
    public UserStatus Status { get; set; } = UserStatus.Active;

    // Global Token Invalidation Barrier (AUTH-SEC-002) - Incremented on password changes/revocations to invalidate existing JWTs
    public int TokenSecurityVersion { get; set; } = 1;

    // Optimistic Concurrency Control (OCC-001) - Monotonically increasing revision counter preventing concurrent state overwrites
    public long Revision { get; set; } = 1;

    // Asynchronous Teardown Barrier - Marks account for background socket eviction and game cancellation upon suspension
    public bool TerminationPending { get; set; }

    // Temporal Auditability - Timestamp when the entity record was created
    public DateTimeOffset CreatedAt { get; set; }

    // Temporal Auditability - Timestamp when the entity record was last updated
    public DateTimeOffset UpdatedAt { get; set; }

    // Actor Auditability - Identifier of the actor that created this record
    public Guid? CreatedBy { get; set; }

    // Actor Auditability - Identifier of the actor that last updated this record
    public Guid? UpdatedBy { get; set; }
}
