namespace Kahoot.Infrastructure.ServiceCollectionExtension;

using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Options;
using Kahoot.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class StorageInstaller
{
    public static IServiceCollection AddStorage(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<ImageStorageOptions>()
            .Bind(configuration.GetSection(ImageStorageOptions.SectionName))
            .Validate(options => options.UploadsSubdirectory == "uploads" && options.StagingSubdirectory == "uploads/staging",
                "ImageStorage paths must match the public uploads route and remain on the same volume.")
            .Validate(options => options.MaxFileSizeBytes == 5_242_880,
                "ImageStorage:MaxFileSizeBytes must match the 5 MiB upload contract.")
            .Validate(options => options.MaxWidth == 4096 && options.MaxHeight == 4096 &&
                                 options.MaxPixelArea == 16_777_216 && options.MaxDecodeMemoryBytes == 67_108_864,
                "ImageStorage image dimensions and decode budget must match the upload contract.")
            .Validate(options => options.MinimumFreeStorageRatio == 0.10,
                "ImageStorage:MinimumFreeStorageRatio must match the 10 percent storage reserve contract.")
            .Validate(options => options.OrphanRetentionDays == 7 && options.StagingQuarantineHours == 24,
                "ImageStorage retention periods must match the image lifecycle contract.")
            .Validate(options => options.CleanupIntervalMinutes > 0 &&
                                 options.CleanupBatchSize is > 0 and <= 1000 &&
                                 options.MaxBatchesPerPass is > 0 and <= 100 &&
                                 options.ReconciliationBatchSize is > 0 and <= 1000,
                "ImageStorage cleanup interval and batch limits must be bounded and positive.")
            .ValidateOnStart();

        services.AddSingleton<IImageStorageService, ImageStorageService>();

        return services;
    }
}
