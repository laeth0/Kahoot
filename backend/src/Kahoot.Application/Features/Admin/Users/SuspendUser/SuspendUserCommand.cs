using Kahoot.Application.Common.Messaging;

namespace Kahoot.Application.Features.Admin.Users.SuspendUser;

public sealed record SuspendUserCommand(
    Guid AccountId,
    // Optimistic Concurrency Fence - Revision required to serialize suspension and detect concurrent mutations (ACCT-BOUND-001)
    long Revision) : ICommand<SuspendUserResult>;
