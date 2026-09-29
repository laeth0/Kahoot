using Kahoot.Domain.Enums;

namespace Kahoot.Application.Features.Admin.Users.GetUserById;

// Administrative Metadata Projection - Exposes host account state without disclosing private quizzes or game history (ACCT-QUERY-002)
public sealed record UserAdminResponse(
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
