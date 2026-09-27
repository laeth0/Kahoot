using Kahoot.Application.Common.Messaging;

namespace Kahoot.Application.Features.Admin.Users.SuspendUser;

public sealed record SuspendUserCommand(
    Guid AccountId,
    long Revision) : ICommand<SuspendUserResult>;
