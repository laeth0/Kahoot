namespace Kahoot.Api.UnitTests.Features.Images;

using System;
using Kahoot.Api.Controllers;
using Kahoot.Api.UnitTests.TestSupport;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

public sealed class ImagePathGuardTests
{
    private readonly RecordingSender _sender;
    private readonly StubImageStorageService _storageService;
    private readonly RecordingLogger<ImagesController> _logger;
    private readonly DefaultHttpContext _httpContext;
    private readonly ImagesController _controller;

    public ImagePathGuardTests()
    {
        _sender = new RecordingSender();
        _storageService = new StubImageStorageService();
        _logger = new RecordingLogger<ImagesController>();
        _httpContext = HttpContextFactory.Create(path: "/uploads");

        _controller = new ImagesController(_sender, _storageService, _logger)
        {
            ControllerContext = new ControllerContext { HttpContext = _httpContext },
            ProblemDetailsFactory = new TestProblemDetailsFactory()
        };
    }

    [Fact]
    public void GetUploadsRoot_Returns404ImageNotFound()
    {
        IActionResult result = _controller.GetUploadsRoot();

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
        ProblemDetails problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(StatusCodes.Status404NotFound, problem.Status);
        Assert.Equal("Image.NotFound", problem.Extensions["code"]?.ToString());
        Assert.Empty(_storageService.RequestedStoragePaths);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("..")]
    [InlineData("../image.jpg")]
    [InlineData("sub/image.jpg")]
    [InlineData("sub\\image.jpg")]
    [InlineData("not-a-guid.jpg")]
    [InlineData("12345.png")]
    [InlineData("e1b59c762e9048e0a434d19e917d2a5a.jpg")]
    [InlineData("e1b59c76-2e90-48e0-a434.jpg")]
    [InlineData("e1b59c76-2e90-48e0-a434-d19e917d2a5a.gif")]
    [InlineData("e1b59c76-2e90-48e0-a434-d19e917d2a5a.bmp")]
    [InlineData("e1b59c76-2e90-48e0-a434-d19e917d2a5a.exe")]
    [InlineData("e1b59c76-2e90-48e0-a434-d19e917d2a5a.txt")]
    [InlineData("e1b59c76-2e90-48e0-a434-d19e917d2a5a.PNG")]
    [InlineData("e1b59c76-2e90-48e0-a434-d19e917d2a5a.JPG")]
    [InlineData("e1b59c76-2e90-48e0-a434-d19e917d2a5a.WEBP")]
    public void GetImage_RejectsInvalidFilenameBeforeStorageResolver(string? filename)
    {
        IActionResult result = _controller.GetImage(filename);

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
        ProblemDetails problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("Image.NotFound", problem.Extensions["code"]?.ToString());
        Assert.Empty(_storageService.RequestedStoragePaths);
    }

    [Theory]
    [InlineData("/uploads/%2e%2e/e1b59c76-2e90-48e0-a434-d19e917d2a5a.jpg")]
    [InlineData("/uploads/%2E%2E/e1b59c76-2e90-48e0-a434-d19e917d2a5a.jpg")]
    [InlineData("/uploads/%2f/e1b59c76-2e90-48e0-a434-d19e917d2a5a.jpg")]
    [InlineData("/uploads/%2F/e1b59c76-2e90-48e0-a434-d19e917d2a5a.jpg")]
    [InlineData("/uploads/%5c/e1b59c76-2e90-48e0-a434-d19e917d2a5a.jpg")]
    [InlineData("/uploads/%5C/e1b59c76-2e90-48e0-a434-d19e917d2a5a.jpg")]
    public void GetImage_RejectsRawPathEncodedTraversalTokensBeforeStorageResolver(string rawPath)
    {
        string validFilename = "e1b59c76-2e90-48e0-a434-d19e917d2a5a.jpg";
        _httpContext.Request.Path = new PathString(rawPath);

        IActionResult result = _controller.GetImage(validFilename);

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
        ProblemDetails problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("Image.NotFound", problem.Extensions["code"]?.ToString());
        Assert.Empty(_storageService.RequestedStoragePaths);
    }

    [Theory]
    [InlineData("e1b59c76-2e90-48e0-a434-d19e917d2a5a.jpg")]
    [InlineData("e1b59c76-2e90-48e0-a434-d19e917d2a5a.jpeg")]
    [InlineData("e1b59c76-2e90-48e0-a434-d19e917d2a5a.png")]
    [InlineData("e1b59c76-2e90-48e0-a434-d19e917d2a5a.webp")]
    [InlineData("E1B59C76-2E90-48E0-A434-D19E917D2A5A.jpg")]
    [InlineData("00000000-0000-0000-0000-000000000000.png")]
    public void GetImage_SyntacticallyAcceptedNames_InvokesResolverAndReturnsNotFoundWhenMissing(string filename)
    {
        _storageService.PhysicalFilePathResolver = _ => null;

        IActionResult result = _controller.GetImage(filename);

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
        ProblemDetails problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("Image.NotFound", problem.Extensions["code"]?.ToString());
        Assert.Contains($"/uploads/{filename}", _storageService.RequestedStoragePaths);
    }
}
