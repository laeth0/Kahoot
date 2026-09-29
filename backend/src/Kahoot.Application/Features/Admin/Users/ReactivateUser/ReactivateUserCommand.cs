using Kahoot.Application.Common.Messaging;

namespace Kahoot.Application.Features.Admin.Users.ReactivateUser;

public sealed record ReactivateUserCommand(
    Guid AccountId,
    // Optimistic Concurrency Fence - Revision required to serialize reactivation and detect concurrent mutations (ACCT-BOUND-001)
    long Revision) : ICommand;
