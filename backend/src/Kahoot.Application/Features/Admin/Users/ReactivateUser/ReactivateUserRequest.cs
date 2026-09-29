namespace Kahoot.Application.Features.Admin.Users.ReactivateUser;

public sealed record ReactivateUserRequest(
    // Optimistic Concurrency Fence - Current revision number expected by administrator to prevent concurrent modifications (ACCT-BOUND-001)
    long Revision);
