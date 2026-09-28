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
            .Validate(options => options.MaxFileSizeBytes > 0, "ImageStorage:MaxFileSizeBytes must be greater than zero.")
            .Validate(options => options.MaxWidth > 0 && options.MaxHeight > 0, "ImageStorage:MaxWidth and MaxHeight must be greater than zero.")
            .Validate(options => options.MaxPixelArea > 0, "ImageStorage:MaxPixelArea must be greater than zero.")
            .Validate(options => options.MinimumFreeStorageRatio > 0 && options.MinimumFreeStorageRatio < 1.0, "ImageStorage:MinimumFreeStorageRatio must be between 0 and 1.")
            .ValidateOnStart();

        services.AddSingleton<IImageStorageService, ImageStorageService>();

        return services;
    }
}
