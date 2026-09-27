using Kahoot.Domain.Enums;

namespace Kahoot.Application.Features.Admin.Users.GetUserById;

public sealed record UserAdminResponse(
    Guid AccountId,
    string Username,
    UserRole AccountKind,
    UserStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StatusChangedAt,
    long Revision,
    bool TerminationPending);
