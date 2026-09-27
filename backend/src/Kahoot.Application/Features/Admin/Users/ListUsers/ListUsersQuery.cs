using Kahoot.Application.Common.Messaging;
using Kahoot.Domain.Enums;

namespace Kahoot.Application.Features.Admin.Users.ListUsers;

public sealed record ListUsersQuery(
    string? Cursor = null,
    int PageSize = 50,
    string? Username = null,
    UserStatus? Status = null) : IQuery<ListUsersResponse>;
