using Kahoot.Domain.Enums;

namespace Kahoot.Domain.Entities;

public sealed class User
{
    public Guid Id { get; set; }

    public required string DisplayUsername { get; set; }

    public required string NormalizedUsername { get; set; }

    public required string PasswordHash { get; set; }

    public UserRole Role { get; set; } = UserRole.Host;

    public UserStatus Status { get; set; } = UserStatus.Active;

    public int TokenSecurityVersion { get; set; } = 1;

    public long Revision { get; set; } = 1;

    public bool TerminationPending { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
