using Kahoot.Application.Common.Messaging;

namespace Kahoot.Application.Features.Admin.Administrators.ListAdministrators;

public sealed record ListAdministratorsQuery : IQuery<IReadOnlyList<AdministratorResponse>>;
