using FluentValidation;
using Kahoot.Application.Features.Images;
using Microsoft.AspNetCore.Diagnostics;

namespace Kahoot.Api.Middleware;

// Global Exception Handler - Centralized RFC 7807 ProblemDetails middleware interceptor normalizing uncaught application exceptions.
internal sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    // Exception Mapping Pipeline - Maps typed domain/infrastructure exceptions to standard HTTP error representations with trace correlations.
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        Exception rootException = exception.GetBaseException();

        // Payload Limit Interception - Converts ASP.NET Core request body size limit rejections into typed image payload error responses.
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

        // Validation Problem Details - Aggregates FluentValidation failures into RFC 7807 validation error dictionaries with 400 Bad Request.
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

        // Argon2id Concurrency Protection - Returns 429 Too Many Requests when password hashing semaphore capacity is exhausted.
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

        // Npgsql pool timeouts can wrap a TimeoutException, so inspect the exception chain before classifying the failure.
        Npgsql.NpgsqlException? databaseException = null;
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is Npgsql.NpgsqlException npgsqlException)
            {
                databaseException = npgsqlException;
                break;
            }
        }

        bool isPoolExhausted = databaseException is not null &&
            (databaseException.Message.Contains("pool has been exhausted", StringComparison.OrdinalIgnoreCase) ||
             databaseException.Message.Contains("connection pool", StringComparison.OrdinalIgnoreCase) ||
             databaseException.SqlState == "53300");

        if (isPoolExhausted)
        {
            _logger.LogError(exception, "Database connection pool exhausted. EventName={EventName}", "DatabasePoolExhausted");
            httpContext.Response.Headers.RetryAfter = "5";

            await Results.Problem(
                instance: httpContext.Request.Path,
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Database.PoolExhausted",
                detail: "Database connection pool capacity exceeded. Please retry after the specified interval.",
                type: "https://api.kahoot-saas.local/errors/Database.PoolExhausted",
                extensions: CreateExtensions(httpContext, "Database.PoolExhausted"))
                .ExecuteAsync(httpContext);

            return true;
        }

        // Transient Fault Shield - Classifies transient Npgsql exceptions and timeouts into 503 Service Unavailable responses with Retry-After: 5 header.
        if (databaseException?.IsTransient == true || rootException is TimeoutException)
        {
            _logger.LogError(exception, "Database service is temporarily unavailable. EventName={EventName}", "DatabaseUnavailable");
            httpContext.Response.Headers.RetryAfter = "5";

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

        // Unhandled Exception Redaction - Catches unexpected errors, logs full stack traces internally, and returns sanitized 500 responses.
        _logger.LogError(exception, "Unhandled request exception");

        await Results.Problem(
            statusCode: StatusCodes.Status500InternalServerError,
            title: "An unexpected error occurred.",
            extensions: CreateExtensions(httpContext)).ExecuteAsync(httpContext);

        return true;
    }

    // Distributed Trace Extensions - Enriches ProblemDetails payloads with HTTP TraceIdentifier and W3C distributed traceId.
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
