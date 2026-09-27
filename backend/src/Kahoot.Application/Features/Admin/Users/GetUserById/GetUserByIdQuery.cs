using Kahoot.Application.Common.Messaging;

namespace Kahoot.Application.Features.Admin.Users.GetUserById;

public sealed record GetUserByIdQuery(Guid AccountId) : IQuery<UserAdminResponse>;
