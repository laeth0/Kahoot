namespace Kahoot.Application.Common.Interfaces;

using Kahoot.Application.Common.Results;

public sealed record SanitizedImageResult(
    Guid ImageId,
    string StoragePath,
    string ContentType,
    long ByteSize,
    int PixelWidth,
    int PixelHeight);

public interface IImageStorageService
{
    // Storage Health Check - Verifies disk space threshold and directory access permissions
    bool IsStorageAvailable();

    // Image Ingestion & Sanitization (IMG-VAL-001, IMG-SEC-002) - Validates magic bytes, decodes, strips EXIF, and re-encodes safely
    Task<Result<SanitizedImageResult>> SanitizeAndPersistAsync(
        Stream sourceStream,
        string originalFileName,
        string? declaredContentType,
        CancellationToken cancellationToken);

    // Atomic Storage Compensation (IMG-STOR-002) - Deletes orphaned file on database commit failure to prevent disk leaks
    void CompensateFile(string storagePath);

    // Physical File Resolver - Resolves relative storage path to local absolute file system path
    string? GetPhysicalFilePath(string storagePath);
}
