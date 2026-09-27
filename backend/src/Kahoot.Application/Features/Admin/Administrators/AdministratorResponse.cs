using Kahoot.Domain.Enums;

namespace Kahoot.Application.Features.Admin.Administrators;

public sealed record AdministratorResponse(
    Guid AccountId,
    string Username,
    UserRole AccountKind,
    UserStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StatusChangedAt,
    long Revision,
    bool TerminationPending);
