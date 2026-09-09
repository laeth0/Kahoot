using Kahoot.Application.Common.Interfaces;
using Kahoot.Domain.Common;
using Microsoft.Extensions.Options;

namespace Kahoot.Application.Common.Storage;

public sealed class ImageUploadService(IFileStorage fileStorage, IOptions<FileStorageOptions> options) : ITransientService
{
    private static readonly Error EmptyFile = new("Upload.EmptyFile", "The uploaded file is empty.");
    private static readonly Error TooLarge = new("Upload.TooLarge", "The uploaded file exceeds the maximum allowed size.");
    private static readonly Error UnsupportedType = new("Upload.UnsupportedType", "The uploaded file type is not supported.");
    private static readonly Error ContentMismatch = new("Upload.ContentMismatch", "The file content does not match an allowed image format.");

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
        string url = await fileStorage.SaveAsync(buffer, ExtensionFor(normalizedContentType), cancellationToken);
        return Result.Success(url);
    }

    private static string ExtensionFor(string contentType) => contentType switch
    {
        "image/jpeg" => ".jpg",
        "image/png" => ".png",
        "image/webp" => ".webp",
        "image/gif" => ".gif",
        _ => ".bin"
    };
}
