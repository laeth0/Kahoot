using Kahoot.Application.Common.Messaging;

namespace Kahoot.Application.Features.Admin.Users.ReactivateUser;

public sealed record ReactivateUserCommand(Guid AccountId, long Revision) : ICommand;
