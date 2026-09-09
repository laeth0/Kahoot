namespace Kahoot.Domain.Common;

public abstract class AuditableEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
