using Kahoot.Application.Common.Messaging;

namespace Kahoot.Application.Features.Admin.Administrators.ReactivateAdministrator;

public sealed record ReactivateAdministratorCommand(Guid AdministratorId, long Revision) : ICommand;
