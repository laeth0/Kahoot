using FluentValidation;
using Kahoot.Application.Features.Images;
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
        if (exception is BadHttpRequestException { StatusCode: StatusCodes.Status413PayloadTooLarge } &&
            string.Equals(httpContext.Request.Path.Value, "/api/uploads/images", StringComparison.OrdinalIgnoreCase))
        {
            await Results.Problem(
                instance: httpContext.Request.Path,
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "Payload too large",
                detail: ImageErrors.TooLarge.Description,
                type: "https://api.kahoot-saas.local/errors/Image.TooLarge",
                extensions: CreateExtensions(httpContext, ImageErrors.TooLarge.Code))
                .ExecuteAsync(httpContext);

            return true;
        }

        if (exception is ValidationException validationException)
        {
            Dictionary<string, string[]> errors = validationException.Errors
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
                extensions: CreateExtensions(httpContext, "Validation.Failed"))
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
                extensions: CreateExtensions(httpContext, "Request.RateLimited"))
                .ExecuteAsync(httpContext);

            return true;
        }

        Exception rootException = exception.GetBaseException();
        if (rootException is Npgsql.NpgsqlException { IsTransient: true } or TimeoutException)
        {
            _logger.LogError(exception, "Database service is temporarily unavailable.");

            await Results.Problem(
                instance: httpContext.Request.Path,
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Service.Unavailable",
                detail: "The service is temporarily unavailable due to a database connection failure.",
                type: "https://api.kahoot-saas.local/errors/Service.Unavailable",
                extensions: CreateExtensions(httpContext, "Service.Unavailable"))
                .ExecuteAsync(httpContext);

            return true;
        }

        _logger.LogError(exception, "Unhandled request exception");

        await Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "An unexpected error occurred.",
            extensions: CreateExtensions(httpContext)).ExecuteAsync(httpContext);

        return true;
    }

    private static Dictionary<string, object?> CreateExtensions(HttpContext httpContext, string? code = null)
    {
        Dictionary<string, object?> extensions = new()
        {
            ["requestId"] = httpContext.TraceIdentifier
        };

        if (!string.IsNullOrEmpty(code))
        {
            extensions["code"] = code;
        }

        if (System.Diagnostics.Activity.Current is not null)
        {
            extensions["traceId"] = System.Diagnostics.Activity.Current.TraceId.ToString();
        }

        return extensions;
    }
}
