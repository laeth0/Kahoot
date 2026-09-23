using Kahoot.Domain.Enums;

namespace Kahoot.Domain.Entities;

public sealed class User
{
    public Guid Id { get; set; }

    public required string Username { get; set; }

    public required string NormalizedUsername { get; set; }

    public required string Email { get; set; }

    public required string Name { get; set; }

    public required string PasswordHash { get; set; }

    public UserRole Role { get; set; } = UserRole.Host;

    public string Status { get; set; } = "Active";

    public int TokenSecurityVersion { get; set; } = 1;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
