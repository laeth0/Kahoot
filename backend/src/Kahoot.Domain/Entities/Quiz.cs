namespace Kahoot.Domain.Entities;

public sealed class Quiz : IAuditableEntity
{
    public Guid Id { get; set; }

    public Guid HostAccountId { get; set; }

    public required string Title { get; set; }

    public string? Description { get; set; }

    public long Revision { get; set; } = 1;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public Guid? CreatedBy { get; set; }

    public Guid? UpdatedBy { get; set; }
}
