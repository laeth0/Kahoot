namespace Kahoot.Domain.Common;

public abstract class AuditableEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
