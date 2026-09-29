namespace Kahoot.Application.Features.Admin.Administrators.ReactivateAdministrator;

public sealed record ReactivateAdministratorRequest(
    // Optimistic Concurrency Fence - Current revision number expected by administrator to prevent concurrent modifications (ACCT-BOUND-001)
    long Revision);
