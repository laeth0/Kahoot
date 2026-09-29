namespace Kahoot.Domain.Entities;

public sealed class QuestionImage
{
    // Primary Identity - Unique identifier for the question image
    public Guid Id { get; set; }

    // Multi-Tenant Isolation (IMG-SEC-001) - Scopes image ownership strictly to the authenticated host account
    public Guid HostAccountId { get; set; }

    // Storage Path (IMG-STOR-001) - Relative physical or object storage path on disk/bucket
    public required string StoragePath { get; set; }

    // MIME Media Type (IMG-VAL-001) - Validated image content type (image/jpeg, image/png, image/webp)
    public required string ContentType { get; set; }

    // Storage Footprint - File payload size in bytes constrained to maximum upload limits (5 MB)
    public long ByteSize { get; set; }

    // Visual Dimension - Width in pixels validated against maximum display resolution boundaries
    public int PixelWidth { get; set; }

    // Visual Dimension - Height in pixels validated against maximum display resolution boundaries
    public int PixelHeight { get; set; }

    // Garbage Collection Lifecycle (IMG-LIFE-001) - Timestamp when image became orphaned; purged after 24h grace period
    public DateTimeOffset? UnreferencedSince { get; set; }

    // Temporal Auditability - Timestamp when the image asset was uploaded
    public DateTimeOffset CreatedAt { get; set; }
}
