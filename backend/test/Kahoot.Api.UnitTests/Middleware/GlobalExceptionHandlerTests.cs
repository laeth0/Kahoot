namespace Kahoot.Api.UnitTests.Middleware;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using FluentValidation.Results;
using Kahoot.Api.Middleware;
using Kahoot.Api.UnitTests.TestSupport;
using Kahoot.Application.Common.Exceptions;
using Kahoot.Application.Features.Images;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

public sealed class GlobalExceptionHandlerTests
{
    private readonly RecordingLogger<GlobalExceptionHandler> _logger = new();
    private readonly GlobalExceptionHandler _handler;

    public GlobalExceptionHandlerTests()
    {
        _handler = new GlobalExceptionHandler(_logger);
    }

    [Fact]
    public async Task TryHandleAsync_MapsValidationFailuresByProperty()
    {
        DefaultHttpContext context = HttpContextFactory.CreateWithLogging(path: "/api/quizzes");
        List<ValidationFailure> failures = new()
        {
            new ValidationFailure("Title", "Title is required."),
            new ValidationFailure("Title", "Title must be at least 3 characters."),
            new ValidationFailure("Description", "Description too long.")
        };
        ValidationException exception = new(failures);

        bool handled = await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Contains("application/problem+json", context.Response.ContentType);

        using JsonDocument document = ParseResponseJson(context);
        JsonElement root = document.RootElement;

        Assert.Equal(400, root.GetProperty("status").GetInt32());
        Assert.Equal("Validation.Failed", root.GetProperty("title").GetString());
        Assert.Equal("Validation.Failed", root.GetProperty("code").GetString());
        Assert.Equal("/api/quizzes", root.GetProperty("instance").GetString());
        Assert.Equal(context.TraceIdentifier, root.GetProperty("requestId").GetString());

        JsonElement errorsElement = root.GetProperty("errors");
        JsonElement titleErrors = errorsElement.GetProperty("Title");
        Assert.Equal(2, titleErrors.GetArrayLength());
        Assert.Equal("Title is required.", titleErrors[0].GetString());
        Assert.Equal("Title must be at least 3 characters.", titleErrors[1].GetString());

        JsonElement descriptionErrors = errorsElement.GetProperty("Description");
        Assert.Equal(1, descriptionErrors.GetArrayLength());
        Assert.Equal("Description too long.", descriptionErrors[0].GetString());
    }

    [Theory]
    [InlineData("/api/uploads/images")]
    [InlineData("/API/UPLOADS/IMAGES")]
    public async Task TryHandleAsync_MapsImagePayloadLimitOnlyAtUploadEndpoint(string path)
    {
        DefaultHttpContext context = HttpContextFactory.CreateWithLogging(path: path);
        BadHttpRequestException exception = new("Request body too large.", StatusCodes.Status413PayloadTooLarge);

        bool handled = await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, context.Response.StatusCode);

        using JsonDocument document = ParseResponseJson(context);
        JsonElement root = document.RootElement;

        Assert.Equal(413, root.GetProperty("status").GetInt32());
        Assert.Equal("Payload too large", root.GetProperty("title").GetString());
        Assert.Equal(ImageErrors.TooLarge.Description, root.GetProperty("detail").GetString());
        Assert.Equal("https://api.kahoot-saas.local/errors/Image.TooLarge", root.GetProperty("type").GetString());
        Assert.Equal(ImageErrors.TooLarge.Code, root.GetProperty("code").GetString());
        Assert.Equal(context.TraceIdentifier, root.GetProperty("requestId").GetString());
    }

    [Fact]
    public async Task TryHandleAsync_ImagePayloadLimitAtOtherPath_FollowsGenericBranch()
    {
        DefaultHttpContext context = HttpContextFactory.CreateWithLogging(path: "/api/other");
        BadHttpRequestException exception = new("Request body too large.", StatusCodes.Status413PayloadTooLarge);

        bool handled = await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);

        using JsonDocument document = ParseResponseJson(context);
        JsonElement root = document.RootElement;

        Assert.Equal(500, root.GetProperty("status").GetInt32());
        Assert.Equal("An unexpected error occurred.", root.GetProperty("title").GetString());
        Assert.False(root.TryGetProperty("code", out _));
    }

    [Fact]
    public async Task TryHandleAsync_MapsPasswordHashingLimit()
    {
        DefaultHttpContext context = HttpContextFactory.CreateWithLogging(path: "/api/auth/login");
        PasswordHashingRateLimitedException exception = new("Limit exceeded.");

        bool handled = await _handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status429TooManyRequests, context.Response.StatusCode);

        using JsonDocument document = ParseResponseJson(context);
        JsonElement root = document.RootElement;

        Assert.Equal(429, root.GetProperty("status").GetInt32());
        Assert.Equal("Request.RateLimited", root.GetProperty("title").GetString());
        Assert.Equal("Password hashing concurrency limit exceeded. Please try again later.", root.GetProperty("detail").GetString());
        Assert.Equal("https://api.kahoot-saas.local/errors/Request.RateLimited", root.GetProperty("type").GetString());
        Assert.Equal("Request.RateLimited", root.GetProperty("code").GetString());
        Assert.Equal(context.TraceIdentifier, root.GetProperty("requestId").GetString());
    }

    [Theory]
    [InlineData("The connection pool has been exhausted, either raise MaxPoolSize or increase Timeout")]
    [InlineData("Connection pool is full")]
    public async Task TryHandleAsync_RecognizesPoolExhaustionThroughWrappers_WithPoolMessage(string poolMessage)
    {
        DefaultHttpContext context = HttpContextFactory.CreateWithLogging(path: "/api/quizzes");
        NpgsqlException npgsqlException = new(poolMessage);
        Exception outerException = new InvalidOperationException("Operation failed while querying database.", npgsqlException);

        bool handled = await _handler.TryHandleAsync(context, outerException, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        Assert.Equal("5", context.Response.Headers.RetryAfter.ToString());

        using JsonDocument document = ParseResponseJson(context);
        JsonElement root = document.RootElement;

        Assert.Equal(503, root.GetProperty("status").GetInt32());
        Assert.Equal("Database.PoolExhausted", root.GetProperty("title").GetString());
        Assert.Equal("Database connection pool capacity exceeded. Please retry after the specified interval.", root.GetProperty("detail").GetString());
        Assert.Equal("Database.PoolExhausted", root.GetProperty("code").GetString());

        Assert.Single(_logger.Entries);
        RecordedLogEntry logEntry = _logger.Entries[0];
        Assert.Equal(LogLevel.Error, logEntry.LogLevel);
        Assert.Same(outerException, logEntry.Exception);
        Assert.Equal("DatabasePoolExhausted", logEntry.GetValue("EventName"));
    }

    [Fact]
    public async Task TryHandleAsync_RecognizesPoolExhaustionThroughWrappers_WithSqlState53300()
    {
        DefaultHttpContext context = HttpContextFactory.CreateWithLogging(path: "/api/quizzes");
        PostgresException postgresException = new("too many connections", "FATAL", "FATAL", "53300");
        Exception outerException = new ApplicationException("Query failed.", postgresException);

        bool handled = await _handler.TryHandleAsync(context, outerException, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        Assert.Equal("5", context.Response.Headers.RetryAfter.ToString());

        using JsonDocument document = ParseResponseJson(context);
        JsonElement root = document.RootElement;

        Assert.Equal(503, root.GetProperty("status").GetInt32());
        Assert.Equal("Database.PoolExhausted", root.GetProperty("title").GetString());
        Assert.Equal("Database.PoolExhausted", root.GetProperty("code").GetString());

        Assert.Single(_logger.Entries);
        RecordedLogEntry logEntry = _logger.Entries[0];
        Assert.Equal(LogLevel.Error, logEntry.LogLevel);
        Assert.Same(outerException, logEntry.Exception);
        Assert.Equal("DatabasePoolExhausted", logEntry.GetValue("EventName"));
    }

    [Fact]
    public async Task TryHandleAsync_ClassifiesPoolBeforeGenericTimeout()
    {
        DefaultHttpContext context = HttpContextFactory.CreateWithLogging(path: "/api/games");
        TimeoutException timeout = new("Operation timed out waiting for connection.");
        NpgsqlException poolException = new("The connection pool has been exhausted", timeout);

        bool handled = await _handler.TryHandleAsync(context, poolException, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        Assert.Equal("5", context.Response.Headers.RetryAfter.ToString());

        using JsonDocument document = ParseResponseJson(context);
        JsonElement root = document.RootElement;

        Assert.Equal(503, root.GetProperty("status").GetInt32());
        Assert.Equal("Database.PoolExhausted", root.GetProperty("title").GetString());
        Assert.Equal("Database.PoolExhausted", root.GetProperty("code").GetString());

        Assert.Single(_logger.Entries);
        Assert.Equal("DatabasePoolExhausted", _logger.Entries[0].GetValue("EventName"));
    }

    [Fact]
    public async Task TryHandleAsync_MapsTransientDatabaseFailureAndRootTimeout()
    {
        DefaultHttpContext context1 = HttpContextFactory.CreateWithLogging(path: "/api/quizzes");
        NpgsqlException transientDbException = new("Transient failure occurred", new TimeoutException("Socket timed out"));
        Assert.True(transientDbException.IsTransient);

        bool handled1 = await _handler.TryHandleAsync(context1, transientDbException, CancellationToken.None);

        Assert.True(handled1);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context1.Response.StatusCode);
        Assert.Equal("5", context1.Response.Headers.RetryAfter.ToString());

        using (JsonDocument document1 = ParseResponseJson(context1))
        {
            JsonElement root1 = document1.RootElement;
            Assert.Equal(503, root1.GetProperty("status").GetInt32());
            Assert.Equal("Service.Unavailable", root1.GetProperty("title").GetString());
            Assert.Equal("The service is temporarily unavailable due to a database connection failure.", root1.GetProperty("detail").GetString());
            Assert.Equal("Service.Unavailable", root1.GetProperty("code").GetString());
        }

        DefaultHttpContext context2 = HttpContextFactory.CreateWithLogging(path: "/api/quizzes");
        TimeoutException rootTimeoutException = new("Root timeout");

        bool handled2 = await _handler.TryHandleAsync(context2, rootTimeoutException, CancellationToken.None);

        Assert.True(handled2);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context2.Response.StatusCode);
        Assert.Equal("5", context2.Response.Headers.RetryAfter.ToString());

        using (JsonDocument document2 = ParseResponseJson(context2))
        {
            JsonElement root2 = document2.RootElement;
            Assert.Equal(503, root2.GetProperty("status").GetInt32());
            Assert.Equal("Service.Unavailable", root2.GetProperty("title").GetString());
            Assert.Equal("Service.Unavailable", root2.GetProperty("code").GetString());
        }

        Assert.Equal(2, _logger.Entries.Count);
        Assert.Equal("DatabaseUnavailable", _logger.Entries[0].GetValue("EventName"));
        Assert.Equal("DatabaseUnavailable", _logger.Entries[1].GetValue("EventName"));
    }

    [Fact]
    public async Task TryHandleAsync_RedactsUnexpectedException()
    {
        DefaultHttpContext context = HttpContextFactory.CreateWithLogging(path: "/api/secret");
        InvalidOperationException unexpectedException = new("Secret connection string Server=db.internal;Password=SuperSecret123;");

        bool handled = await _handler.TryHandleAsync(context, unexpectedException, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);

        using JsonDocument document = ParseResponseJson(context);
        JsonElement root = document.RootElement;

        Assert.Equal(500, root.GetProperty("status").GetInt32());
        Assert.Equal("An unexpected error occurred.", root.GetProperty("title").GetString());
        Assert.Equal(context.TraceIdentifier, root.GetProperty("requestId").GetString());

        Assert.False(root.TryGetProperty("code", out _));
        Assert.False(root.TryGetProperty("detail", out _));

        string rawResponse;
        context.Response.Body.Position = 0;
        using (StreamReader reader = new(context.Response.Body, leaveOpen: true))
        {
            rawResponse = await reader.ReadToEndAsync();
        }

        Assert.DoesNotContain("SuperSecret123", rawResponse);
        Assert.DoesNotContain("db.internal", rawResponse);

        Assert.Single(_logger.Entries);
        Assert.Equal(LogLevel.Error, _logger.Entries[0].LogLevel);
        Assert.Same(unexpectedException, _logger.Entries[0].Exception);
    }

    [Fact]
    public async Task TryHandleAsync_DoesNotMisclassifyPlainPoolMessage()
    {
        DefaultHttpContext context = HttpContextFactory.CreateWithLogging(path: "/api/test");
        InvalidOperationException nonNpgsqlException = new("The connection pool has been exhausted, please wait.");

        bool handled = await _handler.TryHandleAsync(context, nonNpgsqlException, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);

        using JsonDocument document = ParseResponseJson(context);
        JsonElement root = document.RootElement;

        Assert.Equal(500, root.GetProperty("status").GetInt32());
        Assert.Equal("An unexpected error occurred.", root.GetProperty("title").GetString());
        Assert.False(root.TryGetProperty("code", out _));
    }

    [Fact]
    public async Task TryHandleAsync_ReturnsHandledAndCorrelatesActivity()
    {
        Activity? priorActivity = Activity.Current;
        Activity.Current = null;

        try
        {
            DefaultHttpContext contextWithoutActivity = HttpContextFactory.CreateWithLogging(path: "/api/test");
            InvalidOperationException exception1 = new("Plain error without activity");

            bool handled1 = await _handler.TryHandleAsync(contextWithoutActivity, exception1, CancellationToken.None);
            Assert.True(handled1);

            using (JsonDocument document1 = ParseResponseJson(contextWithoutActivity))
            {
                Assert.False(document1.RootElement.TryGetProperty("traceId", out _));
            }

            Activity testActivity = new("GlobalExceptionHandlerTestActivity");
            testActivity.SetIdFormat(ActivityIdFormat.W3C);
            testActivity.Start();

            try
            {
                DefaultHttpContext contextWithActivity = HttpContextFactory.CreateWithLogging(path: "/api/test");
                InvalidOperationException exception2 = new("Error with activity");

                bool handled2 = await _handler.TryHandleAsync(contextWithActivity, exception2, CancellationToken.None);
                Assert.True(handled2);

                using (JsonDocument document2 = ParseResponseJson(contextWithActivity))
                {
                    Assert.True(document2.RootElement.TryGetProperty("traceId", out JsonElement traceIdElement));
                    Assert.Equal(testActivity.TraceId.ToString(), traceIdElement.GetString());
                }
            }
            finally
            {
                testActivity.Stop();
            }
        }
        finally
        {
            Activity.Current = priorActivity;
        }
    }

    private static JsonDocument ParseResponseJson(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return JsonDocument.Parse(context.Response.Body);
    }
}
