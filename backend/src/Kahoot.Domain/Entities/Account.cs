using Kahoot.Domain.Enums;

namespace Kahoot.Domain.Entities;

public sealed class Account
{
    public Guid Id { get; set; }

    public required string DisplayUsername { get; set; }

    public required string NormalizedUsername { get; set; }

    public required string PasswordHash { get; set; }

    public AccountKind AccountKind { get; set; } = AccountKind.Host;

    public AccountStatus Status { get; set; } = AccountStatus.Active;

    public int TokenSecurityVersion { get; set; } = 1;

    public long Revision { get; set; } = 1;

    public bool TerminationPending { get; set; }

    public DateTimeOffset? StatusChangedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
