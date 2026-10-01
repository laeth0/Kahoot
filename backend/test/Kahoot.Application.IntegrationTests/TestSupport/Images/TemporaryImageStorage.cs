namespace Kahoot.Application.IntegrationTests.TestSupport.Images;

using System;
using System.IO;
using System.Threading.Tasks;

public sealed class TemporaryImageStorage : IAsyncDisposable, IDisposable
{
    private bool _isDisposed;

    public TemporaryImageStorage()
    {
        RootDirectory = Path.Combine(Path.GetTempPath(), $"kahoot_it_img_{Guid.NewGuid():N}");
        WebRootDirectory = Path.Combine(RootDirectory, "wwwroot");
        UploadsDirectory = Path.Combine(WebRootDirectory, "uploads");
        StagingDirectory = Path.Combine(UploadsDirectory, "staging");

        Directory.CreateDirectory(UploadsDirectory);
        Directory.CreateDirectory(StagingDirectory);

        HostEnvironment = new TestHostEnvironment(RootDirectory);
    }

    public string RootDirectory { get; }

    public string WebRootDirectory { get; }

    public string UploadsDirectory { get; }

    public string StagingDirectory { get; }

    public TestHostEnvironment HostEnvironment { get; }

    public string GetPhysicalPath(string storagePath)
    {
        string normalized = storagePath.Trim().Replace('\\', '/');
        string fileName = normalized.StartsWith("/uploads/", StringComparison.Ordinal)
            ? normalized["/uploads/".Length..]
            : Path.GetFileName(normalized);

        return Path.Combine(UploadsDirectory, fileName);
    }

    public bool FileExists(string storagePath)
    {
        string physicalPath = GetPhysicalPath(storagePath);
        return File.Exists(physicalPath);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        if (HostEnvironment.ContentRootFileProvider is IDisposable disposableProvider)
        {
            disposableProvider.Dispose();
        }

        if (Directory.Exists(RootDirectory))
        {
            try
            {
                Directory.Delete(RootDirectory, recursive: true);
            }
            catch (IOException exception)
            {
                Console.Error.WriteLine($"[TemporaryImageStorage] Cleanup warning: {exception.Message}");
            }
            catch (UnauthorizedAccessException exception)
            {
                Console.Error.WriteLine($"[TemporaryImageStorage] Cleanup warning: {exception.Message}");
            }
        }
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
