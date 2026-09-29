using Kahoot.Domain.Enums;

namespace Kahoot.Application.Features.Admin.Administrators;

// Administrative Metadata Projection - Exposes platform administrator state without disclosing tenant-scoped data (ACCT-QUERY-002)
public sealed record AdministratorResponse(
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
