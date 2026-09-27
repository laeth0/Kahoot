using Kahoot.Domain.Enums;

namespace Kahoot.Application.Features.Admin.Users.ListUsers;

public sealed record UserAdminItemResponse(
    Guid AccountId,
    string Username,
    UserRole AccountKind,
    UserStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StatusChangedAt,
    long Revision,
    bool TerminationPending);

public sealed record ListUsersResponse(
    IReadOnlyList<UserAdminItemResponse> Items,
    string? NextCursor,
    bool HasMore);
