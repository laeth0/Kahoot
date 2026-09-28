namespace Kahoot.Infrastructure.Storage;

using System.Text;
using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Options;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Images;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

public sealed class ImageStorageService : IImageStorageService
{
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ImageStorageOptions _options;
    private readonly ILogger<ImageStorageService> _logger;

    public ImageStorageService(
        IHostEnvironment hostEnvironment,
        IOptions<ImageStorageOptions> options,
        ILogger<ImageStorageService> logger)
    {
        _hostEnvironment = hostEnvironment;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Result<SanitizedImageResult>> SanitizeAndPersistAsync(
        Stream sourceStream,
        string originalFileName,
        string? declaredContentType,
        CancellationToken cancellationToken)
    {
        string webRoot = Path.Combine(_hostEnvironment.ContentRootPath, "wwwroot");
        string uploadsDir = Path.Combine(webRoot, _options.UploadsSubdirectory);
        string stagingDir = Path.Combine(webRoot, _options.StagingSubdirectory);

        Directory.CreateDirectory(uploadsDir);
        Directory.CreateDirectory(stagingDir);

        if (!HasSufficientFreeDiskSpace(uploadsDir))
        {
            _logger.LogWarning("Storage volume free space is below the required minimum ratio of {Ratio:P0}.", _options.MinimumFreeStorageRatio);
            return Result.Failure<SanitizedImageResult>(ImageErrors.StorageUnavailable);
        }

        MemoryStream memoryStream = new MemoryStream();
        try
        {
            byte[] buffer = new byte[81920];
            int bytesRead;
            long totalRead = 0;

            while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                totalRead += bytesRead;
                if (totalRead > _options.MaxFileSizeBytes)
                {
                    return Result.Failure<SanitizedImageResult>(ImageErrors.TooLarge);
                }

                await memoryStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            }

            if (totalRead == 0)
            {
                return Result.Failure<SanitizedImageResult>(ImageErrors.MissingFile);
            }

            string extension = Path.GetExtension(originalFileName).ToLowerInvariant();
            string normalizedContentType = (declaredContentType ?? string.Empty).Trim().ToLowerInvariant();

            bool isDisallowedExtensionOrMime = extension is ".gif" or ".svg" or ".bmp" or ".tiff" or ".tif" or ".ico" or ".exe" or ".dll" or ".sh" or ".bat" or ".cmd" or ".html" or ".htm" or ".xml" or ".json"
                || normalizedContentType is "image/gif" or "image/svg+xml" or "image/bmp" or "image/tiff" or "image/x-icon" or "application/x-msdownload";

            byte[] bytes = memoryStream.GetBuffer();
            long length = memoryStream.Length;

            bool isGif = length >= 6 && bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x38 && (bytes[4] == 0x37 || bytes[4] == 0x39) && bytes[5] == 0x61;
            bool isBmp = length >= 2 && bytes[0] == 0x42 && bytes[1] == 0x4D;
            bool isTiff = length >= 4 && ((bytes[0] == 0x49 && bytes[1] == 0x49 && bytes[2] == 0x2A && bytes[3] == 0x00) || (bytes[0] == 0x4D && bytes[1] == 0x4D && bytes[2] == 0x00 && bytes[3] == 0x2A));
            bool isExe = length >= 2 && bytes[0] == 0x4D && bytes[1] == 0x5A;
            bool isElf = length >= 4 && bytes[0] == 0x7F && bytes[1] == 0x45 && bytes[2] == 0x4C && bytes[3] == 0x46;
            bool isSvgOrXml = IsSvgOrXmlContent(bytes, length);

            if (isDisallowedExtensionOrMime || isGif || isBmp || isTiff || isExe || isElf || isSvgOrXml)
            {
                return Result.Failure<SanitizedImageResult>(ImageErrors.UnsupportedType);
            }

            bool isJpeg = length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;
            bool isPng = length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 && bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A;
            bool isWebp = length >= 12 && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50;

            bool claimsImage = extension is ".jpg" or ".jpeg" or ".png" or ".webp"
                || normalizedContentType is "image/jpeg" or "image/png" or "image/webp";

            if (!isJpeg && !isPng && !isWebp)
            {
                if (claimsImage)
                {
                    return Result.Failure<SanitizedImageResult>(ImageErrors.InvalidImage);
                }

                return Result.Failure<SanitizedImageResult>(ImageErrors.UnsupportedType);
            }

            memoryStream.Position = 0;
            ImageInfo? imageInfo;
            try
            {
                imageInfo = await Image.IdentifyAsync(memoryStream, cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Image.IdentifyAsync rejected malformed image header.");
                return Result.Failure<SanitizedImageResult>(ImageErrors.InvalidImage);
            }

            if (imageInfo is null)
            {
                return Result.Failure<SanitizedImageResult>(ImageErrors.InvalidImage);
            }

            int width = imageInfo.Width;
            int height = imageInfo.Height;

            if (width < 1 || height < 1 || width > _options.MaxWidth || height > _options.MaxHeight)
            {
                return Result.Failure<SanitizedImageResult>(ImageErrors.InvalidImage);
            }

            long pixelArea = (long)width * height;
            if (pixelArea > _options.MaxPixelArea)
            {
                return Result.Failure<SanitizedImageResult>(ImageErrors.InvalidImage);
            }

            long decodeMemoryBytes = pixelArea * 4L;
            if (decodeMemoryBytes > _options.MaxDecodeMemoryBytes)
            {
                return Result.Failure<SanitizedImageResult>(ImageErrors.InvalidImage);
            }

            memoryStream.Position = 0;
            Image<Rgba32> image;
            try
            {
                image = await Image.LoadAsync<Rgba32>(memoryStream, cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Image.LoadAsync rejected invalid image payload.");
                return Result.Failure<SanitizedImageResult>(ImageErrors.InvalidImage);
            }

            using (image)
            {
                if (image.Frames.Count > 1)
                {
                    while (image.Frames.Count > 1)
                    {
                        image.Frames.RemoveFrame(1);
                    }
                }

                image.Mutate(context => context.AutoOrient());

                image.Metadata.ExifProfile = null;
                image.Metadata.IptcProfile = null;
                image.Metadata.XmpProfile = null;

                IImageEncoder encoder;
                string targetExtension;
                string targetContentType;

                if (isJpeg)
                {
                    encoder = new JpegEncoder { Quality = 85 };
                    targetExtension = ".jpg";
                    targetContentType = "image/jpeg";
                }
                else if (isPng)
                {
                    encoder = new PngEncoder();
                    targetExtension = ".png";
                    targetContentType = "image/png";
                }
                else
                {
                    encoder = new WebpEncoder();
                    targetExtension = ".webp";
                    targetContentType = "image/webp";
                }

                Guid imageId = Guid.NewGuid();
                string destinationFileName = $"{imageId:D}{targetExtension}";
                string relativeStoragePath = $"/uploads/{destinationFileName}";
                string destinationFilePath = Path.Combine(uploadsDir, destinationFileName);

                string stagingFileName = $"{Guid.NewGuid():D}.tmp";
                string stagingFilePath = Path.Combine(stagingDir, stagingFileName);

                try
                {
                    await using (FileStream stagingStream = new FileStream(
                        stagingFilePath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        bufferSize: 81920,
                        useAsync: true))
                    {
                        await image.SaveAsync(stagingStream, encoder, cancellationToken);
                        await stagingStream.FlushAsync(cancellationToken);
                    }

                    File.Move(stagingFilePath, destinationFilePath, overwrite: false);
                }
                catch (IOException ioException)
                {
                    _logger.LogError(ioException, "Disk I/O error occurred during image persistence.");
                    return Result.Failure<SanitizedImageResult>(ImageErrors.StorageUnavailable);
                }
                finally
                {
                    if (File.Exists(stagingFilePath))
                    {
                        try
                        {
                            File.Delete(stagingFilePath);
                        }
                        catch (Exception cleanupException)
                        {
                            _logger.LogWarning(cleanupException, "Failed to clean up staging quarantine file {Path}", stagingFilePath);
                        }
                    }
                }

                long finalByteSize = new FileInfo(destinationFilePath).Length;
                int finalPixelWidth = image.Width;
                int finalPixelHeight = image.Height;

                SanitizedImageResult result = new SanitizedImageResult(
                    imageId,
                    relativeStoragePath,
                    targetContentType,
                    finalByteSize,
                    finalPixelWidth,
                    finalPixelHeight,
                    destinationFilePath);

                return Result.Success(result);
            }
        }
        finally
        {
            await memoryStream.DisposeAsync();
        }
    }

    public void CompensateFile(string storagePath)
    {
        try
        {
            string? physicalPath = GetPhysicalFilePath(storagePath);
            if (physicalPath is not null && File.Exists(physicalPath))
            {
                File.Delete(physicalPath);
                _logger.LogInformation("Compensated and deleted file {Path} for {StoragePath}", physicalPath, storagePath);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to delete file during compensation for storage path {StoragePath}", storagePath);
        }
    }

    public string? GetPhysicalFilePath(string storagePath)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            return null;
        }

        string normalized = storagePath.Trim().Replace('\\', '/');
        if (!normalized.StartsWith("/uploads/"))
        {
            return null;
        }

        string fileName = normalized["/uploads/".Length..];
        if (fileName.Contains("..") || fileName.Contains('/') || fileName.Contains('\\'))
        {
            return null;
        }

        string webRoot = Path.Combine(_hostEnvironment.ContentRootPath, "wwwroot");
        return Path.Combine(webRoot, _options.UploadsSubdirectory, fileName);
    }

    private bool HasSufficientFreeDiskSpace(string targetDirectory)
    {
        try
        {
            string fullPath = Path.GetFullPath(targetDirectory);
            string? root = Path.GetPathRoot(fullPath);
            if (string.IsNullOrEmpty(root))
            {
                return false;
            }

            DriveInfo driveInfo = new DriveInfo(root);
            if (driveInfo.TotalSize <= 0)
            {
                return true;
            }

            double freeRatio = (double)driveInfo.AvailableFreeSpace / driveInfo.TotalSize;
            return freeRatio >= _options.MinimumFreeStorageRatio;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Could not query drive free space for {Directory}. Permitting operation.", targetDirectory);
            return true;
        }
    }

    private static bool IsSvgOrXmlContent(byte[] bytes, long length)
    {
        int checkLength = (int)Math.Min(length, 256);
        string sample = Encoding.UTF8.GetString(bytes, 0, checkLength).TrimStart();
        return sample.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase)
            || sample.StartsWith("<svg", StringComparison.OrdinalIgnoreCase)
            || sample.StartsWith("<!doctype svg", StringComparison.OrdinalIgnoreCase)
            || sample.StartsWith("<html", StringComparison.OrdinalIgnoreCase);
    }
}
