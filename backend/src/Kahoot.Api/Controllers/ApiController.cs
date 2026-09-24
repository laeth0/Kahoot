using Kahoot.Application.Common.Results;
using Microsoft.AspNetCore.Mvc;

namespace Kahoot.Api.Controllers;

[ApiController]
public abstract class ApiController : ControllerBase
{
    protected IActionResult Problem(Error error)
    {
        return error.Type switch
        {
            ErrorType.NotFound => Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: error.Code,
                detail: error.Description,
                extensions: new Dictionary<string, object?> { ["code"] = error.Code }),
            ErrorType.Conflict => Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: error.Code,
                detail: error.Description,
                extensions: new Dictionary<string, object?> { ["code"] = error.Code }),
            ErrorType.Unauthorized => Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: error.Code,
                detail: error.Description,
                extensions: new Dictionary<string, object?> { ["code"] = error.Code }),
            ErrorType.Forbidden => Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: error.Code,
                detail: error.Description,
                extensions: new Dictionary<string, object?> { ["code"] = error.Code }),
            ErrorType.RateLimited => Problem(
                statusCode: StatusCodes.Status429TooManyRequests,
                title: error.Code,
                detail: error.Description,
                extensions: new Dictionary<string, object?> { ["code"] = error.Code }),
            ErrorType.Unavailable => Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: error.Code,
                detail: error.Description,
                extensions: new Dictionary<string, object?> { ["code"] = error.Code }),
            ErrorType.Validation => Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: error.Code,
                detail: error.Description,
                extensions: new Dictionary<string, object?> { ["code"] = error.Code }),
            _ => Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: error.Code,
                detail: error.Description,
                extensions: new Dictionary<string, object?> { ["code"] = error.Code })
        };
    }
}
