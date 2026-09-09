using Kahoot.Application.Common.Errors;
using Kahoot.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace Kahoot.Api.Common;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult ToActionResult(Result result, int successStatusCode = StatusCodes.Status204NoContent)
        => result.IsSuccess ? StatusCode(successStatusCode) : Problem(result.Error);

    protected IActionResult ToActionResult<TValue>(Result<TValue> result, Func<TValue, IActionResult> onSuccess)
        => result.IsSuccess ? onSuccess(result.Value) : Problem(result.Error);

    protected IActionResult Problem(Error error)
    {
        int statusCode = ApiErrorMapping.ToStatusCode(error);

        if (error is ValidationError validationError)
        {
            return ValidationProblem(new ValidationProblemDetails(
                new Dictionary<string, string[]>(validationError.Errors))
            {
                Status = statusCode
            });
        }

        ProblemDetails problemDetails = new()
        {
            Status = statusCode,
            Title = error.Description
        };
        problemDetails.Extensions["code"] = error.Code;

        return new ObjectResult(problemDetails)
        {
            StatusCode = statusCode,
            ContentTypes = { "application/problem+json" }
        };
    }
}
