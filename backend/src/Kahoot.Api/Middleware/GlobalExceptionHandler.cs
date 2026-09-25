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
                instance: httpContext.Request.Path,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Validation.Failed",
                type: "https://api.kahoot-saas.local/errors/Validation.Failed",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "Validation.Failed",
                    ["requestId"] = httpContext.TraceIdentifier
                })
                .ExecuteAsync(httpContext);

            return true;
        }

        if (exception is Kahoot.Application.Common.Exceptions.PasswordHashingRateLimitedException)
        {
            await Results.Problem(
                instance: httpContext.Request.Path,
                statusCode: StatusCodes.Status429TooManyRequests,
                title: "Request.RateLimited",
                detail: "Password hashing concurrency limit exceeded. Please try again later.",
                type: "https://api.kahoot-saas.local/errors/Request.RateLimited",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "Request.RateLimited",
                    ["requestId"] = httpContext.TraceIdentifier
                })
                .ExecuteAsync(httpContext);

            return true;
        }

        var rootException = exception.GetBaseException();
        if (rootException is Npgsql.NpgsqlException { IsTransient: true } or TimeoutException)
        {
            _logger.LogError(exception, "Database service is temporarily unavailable.");

            await Results.Problem(
                instance: httpContext.Request.Path,
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Service.Unavailable",
                detail: "The service is temporarily unavailable due to a database connection failure.",
                type: "https://api.kahoot-saas.local/errors/Service.Unavailable",
                extensions: new Dictionary<string, object?>
                {
                    ["code"] = "Service.Unavailable",
                    ["requestId"] = httpContext.TraceIdentifier
                })
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
