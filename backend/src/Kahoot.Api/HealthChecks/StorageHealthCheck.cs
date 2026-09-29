namespace Kahoot.Api.HealthChecks;

using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

internal sealed class StorageHealthCheck : IHealthCheck
{
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

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_imageStorage.IsStorageAvailable())
            {
                return HealthCheckResult.Degraded("Image storage is unavailable.");
            }

            string stagingDirectory = Path.Combine(
                _environment.ContentRootPath, "wwwroot", _options.StagingSubdirectory);
            string probePath = Path.Combine(stagingDirectory, $".health-{Guid.NewGuid():N}");

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
        catch (Exception)
        {
            return HealthCheckResult.Degraded("Image storage is unavailable.");
        }
    }
}
