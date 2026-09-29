namespace Kahoot.Application.Common.Options;

public sealed class ImageStorageOptions
{
    // Configuration Section Name - Key identifying image storage settings in appsettings
    public const string SectionName = "ImageStorage";

    // Permanent Asset Directory - Filesystem subdirectory storing referenced question images
    public string UploadsSubdirectory { get; set; } = "uploads";

    // Staging Quarantine Directory - Subdirectory holding newly uploaded files pending question association
    public string StagingSubdirectory { get; set; } = "uploads/staging";

    // File Payload Boundary (IMG-LIM-001) - Maximum allowed upload size in bytes (5 MB)
    public long MaxFileSizeBytes { get; set; } = 5_242_880;

    // Pixel Dimension Boundary (IMG-LIM-002) - Maximum allowed horizontal pixel resolution (4096 px)
    public int MaxWidth { get; set; } = 4096;

    // Pixel Dimension Boundary (IMG-LIM-002) - Maximum allowed vertical pixel resolution (4096 px)
    public int MaxHeight { get; set; } = 4096;

    // Pixel Area Safety Limit - Maximum width * height area (16 MP) to prevent decompression bomb attacks
    public long MaxPixelArea { get; set; } = 16_777_216;

    // Decompression Memory Ceiling - Maximum in-memory decode buffer allocation (64 MB)
    public long MaxDecodeMemoryBytes { get; set; } = 67_108_864;

    // Disk Space Safety Ratio - Minimum disk free space percentage required before accepting uploads
    public double MinimumFreeStorageRatio { get; set; } = 0.10;

    // Orphan Retention Window (IMG-LIFE-001) - Grace period in days before unreferenced images are deleted
    public int OrphanRetentionDays { get; set; } = 7;

    // Quarantine Expiration Window - Lifetime of unassociated staging uploads before automated cleanup
    public int StagingQuarantineHours { get; set; } = 24;

    // Periodic Sweep Cadence - Background cleanup worker execution interval in minutes
    public int CleanupIntervalMinutes { get; set; } = 10;

    // Batch Deletion Chunk Size - Records processed per database and filesystem purge transaction
    public int CleanupBatchSize { get; set; } = 100;

    // Batch Pass Throttle - Maximum batches executed per worker wake-up pass to prevent CPU starvation
    public int MaxBatchesPerPass { get; set; } = 20;

    // Storage Reconciliation Batch Size - Number of records processed when synchronizing database rows against disk files
    public int ReconciliationBatchSize { get; set; } = 200;
}
