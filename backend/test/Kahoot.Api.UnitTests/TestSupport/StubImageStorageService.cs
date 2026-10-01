namespace Kahoot.Api.UnitTests.TestSupport;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Results;

public sealed class StubImageStorageService : IImageStorageService
{
    public bool IsStorageAvailableValue { get; set; } = true;

    public Func<bool>? IsStorageAvailableDelegate { get; set; }

    public Func<string, string?>? PhysicalFilePathResolver { get; set; }

    public List<string> RequestedStoragePaths { get; } = new List<string>();

    public bool IsStorageAvailable()
    {
        if (IsStorageAvailableDelegate is not null)
        {
            return IsStorageAvailableDelegate();
        }

        return IsStorageAvailableValue;
    }

    public string? GetPhysicalFilePath(string storagePath)
    {
        RequestedStoragePaths.Add(storagePath);

        if (PhysicalFilePathResolver is not null)
        {
            return PhysicalFilePathResolver(storagePath);
        }

        return null;
    }

    public Task<Result<SanitizedImageResult>> SanitizeAndPersistAsync(
        Stream sourceStream,
        string originalFileName,
        string? declaredContentType,
        CancellationToken cancellationToken)
    {
        throw new InvalidOperationException("SanitizeAndPersistAsync is not supported by StubImageStorageService.");
    }

    public void CompensateFile(string storagePath)
    {
        throw new InvalidOperationException("CompensateFile is not supported by StubImageStorageService.");
    }
}
