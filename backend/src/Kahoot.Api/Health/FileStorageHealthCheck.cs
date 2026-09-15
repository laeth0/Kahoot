using Kahoot.Application.Common.Storage;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Kahoot.Api.Health;

public sealed class FileStorageHealthCheck(
    IWebHostEnvironment environment,
    IOptions<FileStorageOptions> storageOptions) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            string rootPath = storageOptions.Value.ResolveRootPath(environment.ContentRootPath);
            if (!Directory.Exists(rootPath))
            {
                Directory.CreateDirectory(rootPath);
            }

            string testFilePath = Path.Combine(rootPath, $".healthcheck_{Guid.NewGuid():N}");
            File.WriteAllText(testFilePath, "ok");
            File.Delete(testFilePath);

            return Task.FromResult(HealthCheckResult.Healthy("File storage is accessible."));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("File storage is inaccessible.", ex));
        }
    }
}
