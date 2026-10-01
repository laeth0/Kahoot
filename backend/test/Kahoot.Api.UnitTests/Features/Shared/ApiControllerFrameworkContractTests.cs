namespace Kahoot.Api.UnitTests.Features.Shared;

using System;
using System.Diagnostics;
using Kahoot.Api.UnitTests.TestSupport;
using Kahoot.Application.Common.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Options;
using Xunit;

public sealed class ApiControllerFrameworkContractTests
{
    private readonly TestApiController _controller;
    private readonly DefaultHttpContext _httpContext;

    public ApiControllerFrameworkContractTests()
    {
        _httpContext = HttpContextFactory.Create(path: "/api/contract-test", traceIdentifier: "contract-req-id-999");
        _controller = new TestApiController
        {
            ControllerContext = new ControllerContext { HttpContext = _httpContext },
            ProblemDetailsFactory = new DefaultProblemDetailsFactory(Options.Create(new ApiBehaviorOptions()))
        };
    }

    [Fact]
    public void Problem_WithDefaultMvcFactoryWithoutActivity_ReturnsMappedResponse()
    {
        Activity? priorActivity = Activity.Current;
        Activity.Current = null;

        try
        {
            Error error = Error.Validation("Validation.Failed", "Validation error description");

            IActionResult result = _controller.MapProblem(error);

            ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);

            ProblemDetails problemDetails = Assert.IsType<ProblemDetails>(objectResult.Value);
            Assert.Equal(StatusCodes.Status400BadRequest, problemDetails.Status);
            Assert.Equal("Validation.Failed", problemDetails.Extensions["code"]);
            Assert.Equal("contract-req-id-999", problemDetails.Extensions["requestId"]);
        }
        finally
        {
            Activity.Current = priorActivity;
        }
    }

    [Fact]
    public void Problem_WithDefaultMvcFactoryAndActivity_ReturnsMappedResponseWithW3cTraceId()
    {
        Activity? priorActivity = Activity.Current;
        using Activity activity = new Activity("ContractW3cActivity").SetIdFormat(ActivityIdFormat.W3C).Start();

        try
        {
            Error error = Error.Validation("Validation.Failed", "Validation error description");

            IActionResult result = _controller.MapProblem(error);

            ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);

            ProblemDetails problemDetails = Assert.IsType<ProblemDetails>(objectResult.Value);
            Assert.Equal(StatusCodes.Status400BadRequest, problemDetails.Status);
            Assert.Equal("Validation.Failed", problemDetails.Extensions["code"]);
            Assert.Equal("contract-req-id-999", problemDetails.Extensions["requestId"]);
            Assert.Equal(activity.TraceId.ToString(), problemDetails.Extensions["traceId"]);
        }
        finally
        {
            activity.Stop();
            Activity.Current = priorActivity;
        }
    }
}
