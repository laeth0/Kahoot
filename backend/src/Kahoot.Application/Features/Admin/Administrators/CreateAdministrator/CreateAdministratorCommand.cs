using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Features.Admin.Administrators;

namespace Kahoot.Application.Features.Admin.Administrators.CreateAdministrator;

public sealed record CreateAdministratorCommand(string Username, string Password) : ICommand<AdministratorResponse>;
