using Kahoot.Application.Common.Interfaces;
using Kahoot.Domain.Common;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;

namespace Kahoot.Application.Common.Storage;

public sealed class ImageUploadService(IFileStorage fileStorage, IOptions<FileStorageOptions> options)
    : IImageUploadService, ITransientService
{
    private static readonly Error EmptyFile = new("Upload.EmptyFile", "The uploaded file is empty.");
    private static readonly Error TooLarge = new("Upload.TooLarge", "The uploaded file exceeds the maximum allowed size.");
    private static readonly Error UnsupportedType = new("Upload.UnsupportedType", "The uploaded file type is not supported.");
    private static readonly Error ContentMismatch = new("Upload.ContentMismatch", "The file content does not match an allowed image format.");
    private static readonly Error OversizedDimensions = new("Upload.OversizedDimensions", "The uploaded image dimensions exceed the maximum allowed limits.");
    private static readonly Error CorruptImage = new("Upload.CorruptImage", "The uploaded image could not be processed.");

    private readonly FileStorageOptions _options = options.Value;

    public async Task<Result<string>> UploadImageAsync(
        Stream content,
        string? contentType,
        long length,
        CancellationToken cancellationToken)
    {
        if (length <= 0)
        {
            return Result.Failure<string>(EmptyFile);
        }

        if (length > _options.MaxSizeBytes)
        {
            return Result.Failure<string>(TooLarge);
        }

        string normalizedContentType = (contentType ?? string.Empty).Trim().ToLowerInvariant();
        if (!_options.AllowedContentTypes.Contains(normalizedContentType))
        {
            return Result.Failure<string>(UnsupportedType);
        }

        await using MemoryStream buffer = new();
        await content.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        if (!ImageSignature.Matches(buffer, normalizedContentType))
        {
            return Result.Failure<string>(ContentMismatch);
        }

        buffer.Position = 0;
        var imageInfo = await Image.IdentifyAsync(buffer, cancellationToken);
        if (imageInfo is null)
        {
            return Result.Failure<string>(ContentMismatch);
        }

        if (imageInfo.Width > _options.MaxDimensionPixels || imageInfo.Height > _options.MaxDimensionPixels)
        {
            return Result.Failure<string>(OversizedDimensions);
        }

        if ((long)imageInfo.Width * imageInfo.Height > _options.MaxTotalPixels)
        {
            return Result.Failure<string>(OversizedDimensions);
        }

        buffer.Position = 0;
        await using MemoryStream cleanStream = new();

        try
        {
            using Image image = await Image.LoadAsync(buffer, cancellationToken);
            image.Metadata.ExifProfile = null;
            image.Metadata.IptcProfile = null;
            image.Metadata.XmpProfile = null;

            switch (normalizedContentType)
            {
                case "image/jpeg":
                    await image.SaveAsJpegAsync(cleanStream, new JpegEncoder(), cancellationToken);
                    break;
                case "image/png":
                    await image.SaveAsPngAsync(cleanStream, new PngEncoder(), cancellationToken);
                    break;
                case "image/webp":
                    await image.SaveAsWebpAsync(cleanStream, new WebpEncoder(), cancellationToken);
                    break;
                default:
                    return Result.Failure<string>(UnsupportedType);
            }
        }
        catch (UnknownImageFormatException)
        {
            return Result.Failure<string>(ContentMismatch);
        }
        catch (InvalidImageContentException)
        {
            return Result.Failure<string>(CorruptImage);
        }

        cleanStream.Position = 0;
        string url = await fileStorage.SaveAsync(cleanStream, ExtensionFor(normalizedContentType), cancellationToken);
        return Result.Success(url);
    }

    private static string ExtensionFor(string contentType) => contentType switch
    {
        "image/jpeg" => ".jpg",
        "image/png" => ".png",
        "image/webp" => ".webp",
        _ => ".bin"
    };
}
