namespace Kahoot.Domain.Entities;

public interface IAuditableEntity
{
    DateTimeOffset CreatedAt { get; set; }

    DateTimeOffset UpdatedAt { get; set; }
}
