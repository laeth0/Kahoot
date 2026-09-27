using Kahoot.Application.Common.Results;

namespace Kahoot.Application.Features.Admin;

public static class AccountErrors
{
    public static readonly Error NotFound = Error.NotFound(
        "Account.NotFound",
        "Target account ID does not exist or matches wrong account kind.");

    public static readonly Error ConcurrentModification = Error.Conflict(
        "Account.ConcurrentModification",
        "Revision mismatch during status transition.");

    public static readonly Error LastAdministrator = Error.Conflict(
        "Account.LastAdministrator",
        "Attempt to suspend the sole remaining active administrator.");

    public static readonly Error TerminationPending = Error.Conflict(
        "Account.TerminationPending",
        "Attempt to reactivate while suspension finalization is in-flight.");

    public static readonly Error Conflict = Error.Conflict(
        "Account.Conflict",
        "An account with this username already exists.");
}
