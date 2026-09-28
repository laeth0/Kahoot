namespace Kahoot.Api.Controllers;

using System.Text.RegularExpressions;
using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Images;
using Kahoot.Application.Features.Images.UploadImage;
using Kahoot.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

[ApiController]
public sealed class ImagesController : ApiController
{
    private const long MaxUploadBytes = 5_242_880;
    private const long MaxMultipartRequestBytes = MaxUploadBytes + 65_536;

    private static readonly Regex ValidImageFilenameRegex = new(
        @"^[0-9a-fA-F-]{36}\.(jpg|jpeg|png|webp)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly ISender _sender;
    private readonly IImageStorageService _imageStorageService;
    private readonly ILogger<ImagesController> _logger;

    public ImagesController(
        ISender sender,
        IImageStorageService imageStorageService,
        ILogger<ImagesController> logger)
    {
        _sender = sender;
        _imageStorageService = imageStorageService;
        _logger = logger;
    }

    [HttpPost("api/uploads/images")]
    [Authorize(Roles = nameof(UserRole.Host))]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxMultipartRequestBytes)]
    [RequestFormLimits(
        MultipartBodyLengthLimit = MaxMultipartRequestBytes,
        MultipartHeadersLengthLimit = 2_048,
        ValueCountLimit = 1,
        ValueLengthLimit = 1_024)]
    [ProducesResponseType(typeof(UploadImageResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status415UnsupportedMediaType)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> UploadImage(CancellationToken cancellationToken = default)
    {
        if (!_imageStorageService.IsStorageAvailable())
        {
            return Problem(ImageErrors.StorageUnavailable);
        }

        IFormCollection form;
        try
        {
            form = await Request.ReadFormAsync(cancellationToken);
        }
        catch (InvalidDataException)
        {
            return Problem(ImageErrors.MissingFile);
        }
        catch (BadHttpRequestException exception) when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return Problem(ImageErrors.TooLarge);
        }
        catch (BadHttpRequestException)
        {
            return Problem(ImageErrors.MissingFile);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(exception, "Unable to buffer the image upload form.");
            return Problem(ImageErrors.StorageUnavailable);
        }
        IFormFile? file = form.Files.GetFile("file");

        if (form.Files.Count != 1 || file is null || file.Length == 0)
        {
            return Problem(ImageErrors.MissingFile);
        }

        if (file.Length > MaxUploadBytes)
        {
            return Problem(ImageErrors.TooLarge);
        }

        Stream stream;
        try
        {
            stream = file.OpenReadStream();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(exception, "Unable to open the uploaded image stream.");
            return Problem(ImageErrors.StorageUnavailable);
        }

        await using Stream uploadStream = stream;
        UploadImageCommand command = new UploadImageCommand(
            uploadStream,
            file.FileName,
            file.ContentType,
            file.Length);

        Result<UploadImageResponse> result = await _sender.Send(command, cancellationToken);

        if (result.IsSuccess)
        {
            return Created(result.Value.Url, result.Value);
        }

        return Problem(result.Error);
    }

    [HttpGet("uploads")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public IActionResult GetUploadsRoot()
    {
        return Problem(ImageErrors.NotFound);
    }

    [HttpGet("uploads/{*filename}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public IActionResult GetImage([FromRoute] string? filename)
    {
        string rawPath = HttpContext.Request.Path.Value ?? string.Empty;

        if (string.IsNullOrWhiteSpace(filename)
            || rawPath.Contains("%2e%2e", StringComparison.OrdinalIgnoreCase)
            || rawPath.Contains("%2f", StringComparison.OrdinalIgnoreCase)
            || rawPath.Contains("%5c", StringComparison.OrdinalIgnoreCase)
            || filename.Contains("..")
            || filename.Contains('/')
            || filename.Contains('\\')
            || !ValidImageFilenameRegex.IsMatch(filename)
            || !Guid.TryParseExact(Path.GetFileNameWithoutExtension(filename), "D", out _))
        {
            return Problem(ImageErrors.NotFound);
        }

        string storagePath = $"/uploads/{filename}";
        string? physicalPath = _imageStorageService.GetPhysicalFilePath(storagePath);

        if (physicalPath is null)
        {
            return Problem(ImageErrors.NotFound);
        }

        string extension = Path.GetExtension(filename).ToLowerInvariant();
        string contentType = extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };

        FileStream stream;
        try
        {
            stream = new FileStream(
                physicalPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read | FileShare.Delete,
                bufferSize: 65_536,
                useAsync: true);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return Problem(ImageErrors.NotFound);
        }

        Response.Headers.Append("X-Content-Type-Options", "nosniff");
        Response.Headers.Append("Cache-Control", "public, max-age=31536000, immutable");

        return File(stream, contentType);
    }
}
