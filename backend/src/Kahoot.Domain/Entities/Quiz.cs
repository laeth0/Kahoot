namespace Kahoot.Domain.Entities;

public sealed class Quiz
{
    public Guid Id { get; set; }

    public Guid HostAccountId { get; set; }

    public required string Title { get; set; }

    public string? Description { get; set; }

    public bool IsPublished { get; set; }

    public long Revision { get; set; } = 1;

    public DateTimeOffset CreatedAt { get; set; }
}
