using Kahoot.Domain.Enums;

namespace Kahoot.Application.Features.Admin.Users.ListUsers;

// Administrative Metadata Projection - Exposes host account summary without disclosing tenant content (ACCT-QUERY-002)
public sealed record UserAdminItemResponse(
    Guid AccountId,
    string Username,
    UserRole AccountKind,
    UserStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StatusChangedAt,
    // Optimistic Concurrency Token - Monotonic state revision required for concurrent status mutations (ACCT-BOUND-001)
    long Revision,
    // Asynchronous Finalization Marker - Indicates in-flight background game termination status (ACCT-SUSP-004)
    bool TerminationPending);

public sealed record ListUsersResponse(
    IReadOnlyList<UserAdminItemResponse> Items,
    // Opaque Keyset Cursor - Base64-encoded composite cursor (CreatedAt, Id) for constant-time seeks (ACCT-QUERY-001)
    string? NextCursor,
    // Page Existence Flag - Derived from limit+1 over-fetching without requiring a separate COUNT(*) scan
    bool HasMore);
