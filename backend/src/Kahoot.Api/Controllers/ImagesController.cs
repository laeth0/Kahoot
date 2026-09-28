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
    private static readonly Regex ValidImageFilenameRegex = new(
        @"^[0-9a-fA-F-]{36}\.(jpg|jpeg|png|webp)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly ISender _sender;
    private readonly IImageStorageService _imageStorageService;

    public ImagesController(
        ISender sender,
        IImageStorageService imageStorageService)
    {
        _sender = sender;
        _imageStorageService = imageStorageService;
    }

    [HttpPost("api/uploads/images")]
    [Authorize(Roles = nameof(UserRole.Host))]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(UploadImageResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status415UnsupportedMediaType)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> UploadImage(
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
        {
            return Problem(ImageErrors.MissingFile);
        }

        if (file.Length > 5_242_880)
        {
            return Problem(ImageErrors.TooLarge);
        }

        await using Stream stream = file.OpenReadStream();
        UploadImageCommand command = new UploadImageCommand(
            stream,
            file.FileName,
            file.ContentType,
            file.Length);

        Result<UploadImageResponse> result = await _sender.Send(command, cancellationToken);

        if (result.IsSuccess)
        {
            Response.Headers.Location = result.Value.Url;
            return Created(result.Value.Url, result.Value);
        }

        return Problem(result.Error);
    }

    [HttpGet("uploads/{filename}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public IActionResult GetImage([FromRoute] string filename)
    {
        if (string.IsNullOrWhiteSpace(filename) || !ValidImageFilenameRegex.IsMatch(filename))
        {
            return Problem(ImageErrors.NotFound);
        }

        string storagePath = $"/uploads/{filename}";
        string? physicalPath = _imageStorageService.GetPhysicalFilePath(storagePath);

        if (physicalPath is null || !System.IO.File.Exists(physicalPath))
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

        Response.Headers.Append("X-Content-Type-Options", "nosniff");
        Response.Headers.Append("Cache-Control", "public, max-age=31536000, immutable");

        return PhysicalFile(physicalPath, contentType);
    }
}
