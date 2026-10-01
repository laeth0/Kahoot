namespace Kahoot.Api.UnitTests.HealthChecks;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Kahoot.Api.HealthChecks;
using Kahoot.Api.UnitTests.TestSupport;
using Kahoot.Application.Common.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Xunit;

public sealed class StorageHealthCheckGuardTests
{
    private readonly StubHostEnvironment _environment;
    private readonly StubImageStorageService _storageService;
    private readonly IOptions<ImageStorageOptions> _options;
    private readonly StorageHealthCheck _healthCheck;

    public StorageHealthCheckGuardTests()
    {
        _environment = new StubHostEnvironment();
        _storageService = new StubImageStorageService();
        _options = Options.Create(new ImageStorageOptions
        {
            StagingSubdirectory = "uploads/staging"
        });

        _healthCheck = new StorageHealthCheck(_environment, _storageService, _options);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenStorageUnavailable_ReturnsDegradedWithoutFileAccess()
    {
        _storageService.IsStorageAvailableValue = false;

        HealthCheckResult result = await _healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Equal("Image storage is unavailable.", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenStorageThrowsSyntheticException_ReturnsDegradedWithoutLeakingException()
    {
        _storageService.IsStorageAvailableDelegate = () =>
            throw new IOException("Critical underlying disk cluster failure details.");

        HealthCheckResult result = await _healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Equal("Image storage is unavailable.", result.Description);
        Assert.DoesNotContain("Critical underlying disk cluster failure details.", result.Description);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenStorageThrowsOperationCanceledExceptionWithCanceledToken_PropagatesCancellation()
    {
        using CancellationTokenSource cts = new CancellationTokenSource();
        cts.Cancel();

        _storageService.IsStorageAvailableDelegate = () =>
            throw new OperationCanceledException(cts.Token);

        OperationCanceledException thrown = await Assert.ThrowsAsync<OperationCanceledException>(
            () => _healthCheck.CheckHealthAsync(new HealthCheckContext(), cts.Token));

        Assert.Equal(cts.Token, thrown.CancellationToken);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenStorageThrowsCancellationLikeExceptionWithUncanceledToken_ReturnsDegraded()
    {
        _storageService.IsStorageAvailableDelegate = () =>
            throw new OperationCanceledException("Synthetic cancellation thrown without caller token cancellation.");

        HealthCheckResult result = await _healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Equal("Image storage is unavailable.", result.Description);
    }
}
