namespace Kahoot.Api.UnitTests.Features.Shared;

using System;
using System.Diagnostics;
using Kahoot.Api.UnitTests.TestSupport;
using Kahoot.Application.Common.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

public sealed class ApiControllerTests
{
    private readonly TestApiController _controller;
    private readonly DefaultHttpContext _httpContext;

    public ApiControllerTests()
    {
        _httpContext = HttpContextFactory.Create(path: "/api/test-endpoint", traceIdentifier: "req-trace-id-12345");
        _controller = new TestApiController
        {
            ControllerContext = new ControllerContext { HttpContext = _httpContext },
            ProblemDetailsFactory = new TestProblemDetailsFactory()
        };
    }

    [Theory]
    [InlineData(ErrorType.NotFound, StatusCodes.Status404NotFound)]
    [InlineData(ErrorType.Conflict, StatusCodes.Status409Conflict)]
    [InlineData(ErrorType.Unauthorized, StatusCodes.Status401Unauthorized)]
    [InlineData(ErrorType.Forbidden, StatusCodes.Status403Forbidden)]
    [InlineData(ErrorType.RateLimited, StatusCodes.Status429TooManyRequests)]
    [InlineData(ErrorType.Unavailable, StatusCodes.Status503ServiceUnavailable)]
    [InlineData(ErrorType.Validation, StatusCodes.Status400BadRequest)]
    [InlineData(ErrorType.TooLarge, StatusCodes.Status413PayloadTooLarge)]
    [InlineData(ErrorType.UnsupportedType, StatusCodes.Status415UnsupportedMediaType)]
    [InlineData(ErrorType.Failure, StatusCodes.Status500InternalServerError)]
    public void Problem_MapsErrorTypeToHttpStatus(ErrorType errorType, int expectedStatusCode)
    {
        Error error = new Error("Test.ErrorCode", "Test description", errorType);

        IActionResult result = _controller.MapProblem(error);

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(expectedStatusCode, objectResult.StatusCode);

        ProblemDetails problemDetails = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(expectedStatusCode, problemDetails.Status);
    }

    [Theory]
    [InlineData("Auth.RefreshRace", "Concurrent refresh")]
    [InlineData("Validation.Failed", "Validation failed")]
    [InlineData("Image.TooLarge", "Payload too large")]
    [InlineData("Game.InvalidStateTransition", "Invalid state transition")]
    [InlineData("Custom.UnmappedCode", "Custom.UnmappedCode")]
    public void Problem_MapsKnownTitlesAndFallsBackToCode(string errorCode, string expectedTitle)
    {
        Error error = new Error(errorCode, "Sample description", ErrorType.Validation);

        IActionResult result = _controller.MapProblem(error);

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        ProblemDetails problemDetails = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(expectedTitle, problemDetails.Title);
    }

    [Fact]
    public void Problem_PreservesDescriptionInstanceAndErrorTypeUri()
    {
        const string code = "Quiz.NotFound";
        const string description = "The requested quiz was not found.";
        Error error = Error.NotFound(code, description);

        IActionResult result = _controller.MapProblem(error);

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);

        ProblemDetails problemDetails = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(StatusCodes.Status404NotFound, problemDetails.Status);
        Assert.Equal(description, problemDetails.Detail);
        Assert.Equal("/api/test-endpoint", problemDetails.Instance);
        Assert.Equal($"https://api.kahoot-saas.local/errors/{code}", problemDetails.Type);
    }

    [Fact]
    public void Problem_AddsRequestIdAndCurrentW3cTraceId()
    {
        Activity? priorActivity = Activity.Current;
        using Activity activity = new Activity("UnitTestingActivity").SetIdFormat(ActivityIdFormat.W3C).Start();

        try
        {
            Error error = Error.Validation("Validation.Failed", "Invalid input");

            IActionResult result = _controller.MapProblem(error);

            ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
            ProblemDetails problemDetails = Assert.IsType<ProblemDetails>(objectResult.Value);

            Assert.True(problemDetails.Extensions.TryGetValue("code", out object? codeObj));
            Assert.Equal("Validation.Failed", codeObj);

            Assert.True(problemDetails.Extensions.TryGetValue("requestId", out object? requestIdObj));
            Assert.Equal("req-trace-id-12345", requestIdObj);

            Assert.True(problemDetails.Extensions.TryGetValue("traceId", out object? traceIdObj));
            Assert.Equal(activity.TraceId.ToString(), traceIdObj);
        }
        finally
        {
            activity.Stop();
            Activity.Current = priorActivity;
        }
    }

    [Fact]
    public void Problem_DoesNotInventTraceIdWithoutActivity()
    {
        Activity? priorActivity = Activity.Current;
        Activity.Current = null;

        try
        {
            Error error = Error.Validation("Validation.Failed", "Invalid input");

            IActionResult result = _controller.MapProblem(error);

            ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
            ProblemDetails problemDetails = Assert.IsType<ProblemDetails>(objectResult.Value);

            Assert.True(problemDetails.Extensions.TryGetValue("code", out object? codeObj));
            Assert.Equal("Validation.Failed", codeObj);

            Assert.True(problemDetails.Extensions.TryGetValue("requestId", out object? requestIdObj));
            Assert.Equal("req-trace-id-12345", requestIdObj);

            Assert.False(problemDetails.Extensions.ContainsKey("traceId"));
        }
        finally
        {
            Activity.Current = priorActivity;
        }
    }
}
