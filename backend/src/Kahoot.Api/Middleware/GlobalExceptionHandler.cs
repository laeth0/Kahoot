using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;

namespace Kahoot.Api.Middleware;

internal sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is ValidationException validationException)
        {
            var errors = validationException.Errors
                .GroupBy(error => error.PropertyName)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(error => error.ErrorMessage).ToArray());

            await Results.ValidationProblem(
                errors,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Validation.Failed",
                extensions: new Dictionary<string, object?> { ["code"] = "Validation.Failed" })
                .ExecuteAsync(httpContext);

            return true;
        }

        if (exception is Npgsql.NpgsqlException npgsqlException && npgsqlException.IsTransient
            || exception is TimeoutException)
        {
            _logger.LogError(exception, "Database service is temporarily unavailable.");

            await Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Service.Unavailable",
                detail: "The service is temporarily unavailable due to a database connection failure.",
                extensions: new Dictionary<string, object?> { ["code"] = "Service.Unavailable" })
                .ExecuteAsync(httpContext);

            return true;
        }

        _logger.LogError(exception, "Unhandled request exception");

        await Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "An unexpected error occurred.").ExecuteAsync(httpContext);

        return true;
    }
}
