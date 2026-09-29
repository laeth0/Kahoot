namespace Kahoot.Application.Features.Admin.Users.SuspendUser;

public sealed record SuspendUserRequest(
    // Optimistic Concurrency Fence - Current revision number expected by administrator to prevent concurrent modifications (ACCT-BOUND-001)
    long Revision);
