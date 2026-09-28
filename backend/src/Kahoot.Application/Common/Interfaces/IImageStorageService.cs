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
    bool IsStorageAvailable();

    Task<Result<SanitizedImageResult>> SanitizeAndPersistAsync(
        Stream sourceStream,
        string originalFileName,
        string? declaredContentType,
        CancellationToken cancellationToken);

    void CompensateFile(string storagePath);

    string? GetPhysicalFilePath(string storagePath);
}
