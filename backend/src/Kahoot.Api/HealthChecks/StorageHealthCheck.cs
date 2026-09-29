namespace Kahoot.Api.HealthChecks;

using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

// Storage Health Check - Verifies disk availability, free volume thresholds, and write-permission probes for image assets.
internal sealed class StorageHealthCheck : IHealthCheck
{
    // Ephemeral Probe Buffer - Single-byte payload used to test low-overhead disk write operations.
    private static readonly byte[] ProbeContent = [0];

    private readonly IHostEnvironment _environment;
    private readonly IImageStorageService _imageStorage;
    private readonly ImageStorageOptions _options;

    public StorageHealthCheck(
        IHostEnvironment environment,
        IImageStorageService imageStorage,
        IOptions<ImageStorageOptions> options)
    {
        _environment = environment;
        _imageStorage = imageStorage;
        _options = options.Value;
    }

    // Disk Storage Probe - Tests image storage accessibility and performs zero-leak write-and-delete file tests.
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Storage Capacity Check - Ensures upload target directory exists and maintains minimum 10% disk free space.
            if (!_imageStorage.IsStorageAvailable())
            {
                return HealthCheckResult.Degraded("Image storage is unavailable.");
            }

            string stagingDirectory = Path.Combine(
                _environment.ContentRootPath, "wwwroot", _options.StagingSubdirectory);
            string probePath = Path.Combine(stagingDirectory, $".health-{Guid.NewGuid():N}");

            // Ephemeral File Probe - Uses DeleteOnClose flag to ensure OS immediately removes probe file upon disposal.
            await using FileStream probe = new(
                probePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 1,
                FileOptions.Asynchronous | FileOptions.DeleteOnClose);
            await probe.WriteAsync(ProbeContent, cancellationToken);
            await probe.FlushAsync(cancellationToken);

            return HealthCheckResult.Healthy();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        // Degraded Health Status - Marks storage degraded rather than unhealthy so live gameplay can proceed if image uploads fail.
        catch (Exception)
        {
            return HealthCheckResult.Degraded("Image storage is unavailable.");
        }
    }
}
