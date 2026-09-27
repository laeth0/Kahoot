using Kahoot.Application.Common.Messaging;

namespace Kahoot.Application.Features.Admin.Administrators.SuspendAdministrator;

public sealed record SuspendAdministratorCommand(Guid AdministratorId, long Revision) : ICommand;
