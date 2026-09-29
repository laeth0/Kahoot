using Kahoot.Application.Common.Results;

namespace Kahoot.Application.Features.Admin;

public static class AccountErrors
{
    // Tenant Isolation & Identity Boundary - Emitted when target account identifier does not exist or matches wrong account role (ACCT-ERR-004)
    public static readonly Error NotFound = Error.NotFound(
        "Account.NotFound",
        "Target account ID does not exist or matches wrong account kind.");

    // Optimistic Concurrency Control (OCC) - Detects state drift or concurrent administrative mutations via revision mismatch (ACCT-BOUND-001, ACCT-ERR-005)
    public static readonly Error ConcurrentModification = Error.Conflict(
        "Account.ConcurrentModification",
        "Revision mismatch during status transition.");

    // High Availability Administrative Invariant - Prevents platform lockout by guarding the final active SystemAdmin account (ACCT-ADMIN-003, ACCT-BOUND-004, ACCT-RISK-003)
    public static readonly Error LastAdministrator = Error.Conflict(
        "Account.LastAdministrator",
        "Attempt to suspend the sole remaining active administrator.");

    // Reactivation Gate Barrier - Blocks account reactivation while Phase 2 asynchronous game termination is in-flight (ACCT-BOUND-005, ACCT-ERR-007, ACCT-RISK-005)
    public static readonly Error TerminationPending = Error.Conflict(
        "Account.TerminationPending",
        "Attempt to reactivate while suspension finalization is in-flight.");

    // Canonical Unique Identity Invariant - Rejects duplicate administrator registrations colliding on normalized username (ACCT-ADMIN-001)
    public static readonly Error Conflict = Error.Conflict(
        "Account.Conflict",
        "An account with this username already exists.");
}
