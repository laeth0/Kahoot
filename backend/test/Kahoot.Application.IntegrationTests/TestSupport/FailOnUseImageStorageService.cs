namespace Kahoot.Application.IntegrationTests.TestSupport;

using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Results;

public sealed class FailOnUseImageStorageService : IImageStorageService
{
    public bool IsStorageAvailable()
    {
        throw new NotSupportedException("Image storage is not supported in this test phase.");
    }

    public Task<Result<SanitizedImageResult>> SanitizeAndPersistAsync(
        Stream sourceStream,
        string originalFileName,
        string? declaredContentType,
        CancellationToken cancellationToken)
    {
        throw new NotSupportedException("Image storage is not supported in this test phase.");
    }

    public void CompensateFile(string storagePath)
    {
        throw new NotSupportedException("Image storage is not supported in this test phase.");
    }

    public string? GetPhysicalFilePath(string storagePath)
    {
        throw new NotSupportedException("Image storage is not supported in this test phase.");
    }
}
