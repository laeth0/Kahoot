using Kahoot.Application.Common.Messaging;

namespace Kahoot.Application.Features.Admin.Administrators.ReactivateAdministrator;

public sealed record ReactivateAdministratorCommand(
    Guid AdministratorId,
    // Optimistic Concurrency Fence - Revision required to serialize reactivation and detect concurrent mutations (ACCT-BOUND-001)
    long Revision) : ICommand;
