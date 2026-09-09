using Kahoot.Application.Common.Storage;
using Kahoot.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace Kahoot.Api.Controllers;

[ApiController]
[Route("api/uploads")]
public sealed class UploadsController(ImageUploadService imageUploadService) : ControllerBase
{
    [HttpPost("images")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ImageUploadResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadImage(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "No file was provided.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        await using Stream content = file.OpenReadStream();

        Result<string> result = await imageUploadService.UploadImageAsync(
            content,
            file.ContentType,
            file.Length,
            cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(new ProblemDetails
            {
                Title = result.Error.Description,
                Status = StatusCodes.Status400BadRequest,
                Extensions = { ["code"] = result.Error.Code }
            });
        }

        return Created(result.Value, new ImageUploadResponse(result.Value));
    }
}

public sealed record ImageUploadResponse(string Url);
