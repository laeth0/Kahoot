using Kahoot.Application.Common.Messaging;

namespace Kahoot.Application.Features.Admin.Administrators.SuspendAdministrator;

public sealed record SuspendAdministratorCommand(
    Guid AdministratorId,
    // Optimistic Concurrency Fence - Revision required to serialize suspension and detect concurrent mutations (ACCT-BOUND-001)
    long Revision) : ICommand;
