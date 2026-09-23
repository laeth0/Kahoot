using Kahoot.Domain.Common;

namespace Kahoot.Domain.Entities;

public sealed class User : IEntity
{
    public Guid Id { get; set; }

    public required string Email { get; set; }

    public required string Name { get; set; }

    public required string PasswordHash { get; set; }
}
