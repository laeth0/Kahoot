namespace Kahoot.Application.Common.Options;

public sealed class ImageStorageOptions
{
    public const string SectionName = "ImageStorage";

    public string UploadsSubdirectory { get; set; } = "uploads";

    public string StagingSubdirectory { get; set; } = "uploads/staging";

    public long MaxFileSizeBytes { get; set; } = 5_242_880;

    public int MaxWidth { get; set; } = 4096;

    public int MaxHeight { get; set; } = 4096;

    public long MaxPixelArea { get; set; } = 16_777_216;

    public long MaxDecodeMemoryBytes { get; set; } = 67_108_864;

    public double MinimumFreeStorageRatio { get; set; } = 0.10;

    public int OrphanRetentionDays { get; set; } = 7;

    public int StagingQuarantineHours { get; set; } = 24;

    public int CleanupIntervalMinutes { get; set; } = 10;

    public int CleanupBatchSize { get; set; } = 100;

    public int MaxBatchesPerPass { get; set; } = 20;

    public int ReconciliationBatchSize { get; set; } = 200;
}
