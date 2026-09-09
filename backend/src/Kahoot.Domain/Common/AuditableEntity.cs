namespace Kahoot.Domain.Common;

public abstract class AuditableEntity
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }
}
