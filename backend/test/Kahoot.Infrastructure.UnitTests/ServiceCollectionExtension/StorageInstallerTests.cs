namespace Kahoot.Infrastructure.UnitTests.ServiceCollectionExtension;

using System;
using System.Collections.Generic;
using System.Linq;
using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Options;
using Kahoot.Infrastructure.ServiceCollectionExtension;
using Kahoot.Infrastructure.Storage;
using Kahoot.Infrastructure.UnitTests.TestSupport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

public sealed class StorageInstallerTests
{
    [Fact]
    public void AddStorage_RegistersSingletonImageStorageServiceDescriptor()
    {
        ServiceCollection services = new();
        IConfiguration configuration = InfrastructureConfiguration.BuildConfiguration(
            InfrastructureConfiguration.CreateValidConfigurationDictionary());

        services.AddStorage(configuration);

        ServiceDescriptor? descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IImageStorageService));
        Assert.NotNull(descriptor);
        Assert.Equal(typeof(ImageStorageService), descriptor.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void AddStorage_DefaultOptions_ResolvesSuccessfully()
    {
        ServiceCollection services = new();
        IConfiguration configuration = InfrastructureConfiguration.BuildConfiguration(
            InfrastructureConfiguration.CreateValidConfigurationDictionary());

        services.AddStorage(configuration);
        using ServiceProvider provider = services.BuildServiceProvider();

        ImageStorageOptions options = provider.GetRequiredService<IOptions<ImageStorageOptions>>().Value;
        Assert.Equal("uploads", options.UploadsSubdirectory);
        Assert.Equal("uploads/staging", options.StagingSubdirectory);
        Assert.Equal(5_242_880, options.MaxFileSizeBytes);
        Assert.Equal(4096, options.MaxWidth);
        Assert.Equal(4096, options.MaxHeight);
        Assert.Equal(16_777_216, options.MaxPixelArea);
        Assert.Equal(67_108_864, options.MaxDecodeMemoryBytes);
        Assert.Equal(0.10, options.MinimumFreeStorageRatio);
        Assert.Equal(7, options.OrphanRetentionDays);
        Assert.Equal(24, options.StagingQuarantineHours);
    }

    [Theory]
    [InlineData("ImageStorage:UploadsSubdirectory", "custom_uploads")]
    [InlineData("ImageStorage:StagingSubdirectory", "uploads/temp")]
    [InlineData("ImageStorage:MaxFileSizeBytes", "1048576")]
    [InlineData("ImageStorage:MaxWidth", "2048")]
    [InlineData("ImageStorage:MaxHeight", "2048")]
    [InlineData("ImageStorage:MaxPixelArea", "8388608")]
    [InlineData("ImageStorage:MaxDecodeMemoryBytes", "33554432")]
    [InlineData("ImageStorage:MinimumFreeStorageRatio", "0.05")]
    [InlineData("ImageStorage:OrphanRetentionDays", "14")]
    [InlineData("ImageStorage:StagingQuarantineHours", "48")]
    [InlineData("ImageStorage:CleanupIntervalMinutes", "0")]
    [InlineData("ImageStorage:CleanupIntervalMinutes", "-1")]
    [InlineData("ImageStorage:CleanupBatchSize", "0")]
    [InlineData("ImageStorage:CleanupBatchSize", "1001")]
    [InlineData("ImageStorage:ReconciliationBatchSize", "0")]
    [InlineData("ImageStorage:ReconciliationBatchSize", "1001")]
    [InlineData("ImageStorage:MaxBatchesPerPass", "0")]
    [InlineData("ImageStorage:MaxBatchesPerPass", "101")]
    public void AddStorage_MutatedOptionViolatingContract_ThrowsOptionsValidation(string configKey, string configValue)
    {
        Dictionary<string, string?> config = InfrastructureConfiguration.CreateValidConfigurationDictionary();
        config[configKey] = configValue;

        ServiceCollection services = new();
        services.AddStorage(InfrastructureConfiguration.BuildConfiguration(config));
        using ServiceProvider provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() =>
        {
            _ = provider.GetRequiredService<IOptions<ImageStorageOptions>>().Value;
        });
    }

    [Theory]
    [InlineData("ImageStorage:CleanupBatchSize", 1)]
    [InlineData("ImageStorage:CleanupBatchSize", 1000)]
    [InlineData("ImageStorage:ReconciliationBatchSize", 1)]
    [InlineData("ImageStorage:ReconciliationBatchSize", 1000)]
    [InlineData("ImageStorage:MaxBatchesPerPass", 1)]
    [InlineData("ImageStorage:MaxBatchesPerPass", 100)]
    public void AddStorage_ValidBoundaryValues_Accepted(string configKey, int validValue)
    {
        Dictionary<string, string?> config = InfrastructureConfiguration.CreateValidConfigurationDictionary();
        config[configKey] = validValue.ToString();

        ServiceCollection services = new();
        services.AddStorage(InfrastructureConfiguration.BuildConfiguration(config));
        using ServiceProvider provider = services.BuildServiceProvider();

        ImageStorageOptions options = provider.GetRequiredService<IOptions<ImageStorageOptions>>().Value;
        Assert.NotNull(options);
    }
}
