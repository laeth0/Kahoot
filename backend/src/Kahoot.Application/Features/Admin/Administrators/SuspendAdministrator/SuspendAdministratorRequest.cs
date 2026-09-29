namespace Kahoot.Application.Features.Admin.Administrators.SuspendAdministrator;

public sealed record SuspendAdministratorRequest(
    // Optimistic Concurrency Fence - Current revision number expected by administrator to prevent concurrent modifications (ACCT-BOUND-001)
    long Revision);
