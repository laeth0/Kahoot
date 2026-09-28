namespace Kahoot.Domain.Entities;

public sealed class QuestionImage
{
    public Guid Id { get; set; }

    public Guid HostAccountId { get; set; }

    public required string StoragePath { get; set; }

    public required string ContentType { get; set; }

    public long ByteSize { get; set; }

    public int PixelWidth { get; set; }

    public int PixelHeight { get; set; }

    public DateTimeOffset? UnreferencedSince { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
