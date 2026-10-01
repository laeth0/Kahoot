namespace Kahoot.Api.UnitTests.Features.Images;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Kahoot.Api.Controllers;
using Kahoot.Api.UnitTests.TestSupport;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Images;
using Kahoot.Application.Features.Images.UploadImage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Xunit;

public sealed class ImageUploadControllerTests
{
    private readonly RecordingSender _sender;
    private readonly StubImageStorageService _storageService;
    private readonly RecordingLogger<ImagesController> _logger;
    private readonly DefaultHttpContext _httpContext;
    private readonly ImagesController _controller;

    public ImageUploadControllerTests()
    {
        _sender = new RecordingSender();
        _storageService = new StubImageStorageService();
        _logger = new RecordingLogger<ImagesController>();
        _httpContext = HttpContextFactory.Create(path: "/api/uploads/images");
        _httpContext.Request.ContentType = "multipart/form-data; boundary=---------------------------974767299852498929531610575";

        _controller = new ImagesController(_sender, _storageService, _logger)
        {
            ControllerContext = new ControllerContext { HttpContext = _httpContext },
            ProblemDetailsFactory = new TestProblemDetailsFactory()
        };
    }

    [Fact]
    public async Task UploadImage_UnavailableStorageRejectsBeforeFormRead()
    {
        _storageService.IsStorageAvailableValue = false;
        StubFormFeature formFeature = new StubFormFeature
        {
            ExceptionToThrowOnRead = new InvalidOperationException("Form must not be read when storage is unavailable.")
        };
        _httpContext.Features.Set<IFormFeature>(formFeature);

        IActionResult result = await _controller.UploadImage(CancellationToken.None);

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);
        ProblemDetails problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, problem.Status);
        Assert.Equal("Image.StorageUnavailable", problem.Extensions["code"]?.ToString());
        Assert.False(formFeature.WasReadFormAsyncCalled);
        Assert.Empty(_sender.Requests);
    }

    [Fact]
    public async Task UploadImage_MapsFormReadFailure_InvalidDataException_Returns400ValidationFailed()
    {
        StubFormFeature formFeature = new StubFormFeature
        {
            ExceptionToThrowOnRead = new InvalidDataException("Invalid form data.")
        };
        _httpContext.Features.Set<IFormFeature>(formFeature);

        IActionResult result = await _controller.UploadImage(CancellationToken.None);

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
        ProblemDetails problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Equal("Validation.Failed", problem.Extensions["code"]?.ToString());
        Assert.Empty(_sender.Requests);
    }

    [Fact]
    public async Task UploadImage_MapsFormReadFailure_BadHttpRequestException413_Returns413TooLarge()
    {
        StubFormFeature formFeature = new StubFormFeature
        {
            ExceptionToThrowOnRead = new BadHttpRequestException("Payload too large", StatusCodes.Status413PayloadTooLarge)
        };
        _httpContext.Features.Set<IFormFeature>(formFeature);

        IActionResult result = await _controller.UploadImage(CancellationToken.None);

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, objectResult.StatusCode);
        ProblemDetails problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, problem.Status);
        Assert.Equal("Image.TooLarge", problem.Extensions["code"]?.ToString());
        Assert.Empty(_sender.Requests);
    }

    [Fact]
    public async Task UploadImage_MapsFormReadFailure_OtherBadHttpRequestException_Returns400ValidationFailed()
    {
        StubFormFeature formFeature = new StubFormFeature
        {
            ExceptionToThrowOnRead = new BadHttpRequestException("Bad request", StatusCodes.Status400BadRequest)
        };
        _httpContext.Features.Set<IFormFeature>(formFeature);

        IActionResult result = await _controller.UploadImage(CancellationToken.None);

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
        ProblemDetails problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Equal("Validation.Failed", problem.Extensions["code"]?.ToString());
        Assert.Empty(_sender.Requests);
    }

    [Fact]
    public async Task UploadImage_MapsFormReadFailure_IOException_Returns503AndLogsError()
    {
        IOException ioException = new IOException("Disk buffer failed");
        StubFormFeature formFeature = new StubFormFeature
        {
            ExceptionToThrowOnRead = ioException
        };
        _httpContext.Features.Set<IFormFeature>(formFeature);

        IActionResult result = await _controller.UploadImage(CancellationToken.None);

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);
        ProblemDetails problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, problem.Status);
        Assert.Equal("Image.StorageUnavailable", problem.Extensions["code"]?.ToString());
        Assert.Empty(_sender.Requests);

        Assert.Contains(_logger.Entries, entry => entry.LogLevel == Microsoft.Extensions.Logging.LogLevel.Error
            && ReferenceEquals(entry.Exception, ioException)
            && entry.FormattedMessage.Contains("Unable to buffer the image upload form."));
    }

    [Fact]
    public async Task UploadImage_MapsFormReadFailure_UnauthorizedAccessException_Returns503AndLogsError()
    {
        UnauthorizedAccessException accessException = new UnauthorizedAccessException("Buffer permission denied");
        StubFormFeature formFeature = new StubFormFeature
        {
            ExceptionToThrowOnRead = accessException
        };
        _httpContext.Features.Set<IFormFeature>(formFeature);

        IActionResult result = await _controller.UploadImage(CancellationToken.None);

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);
        ProblemDetails problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, problem.Status);
        Assert.Equal("Image.StorageUnavailable", problem.Extensions["code"]?.ToString());
        Assert.Empty(_sender.Requests);

        Assert.Contains(_logger.Entries, entry => entry.LogLevel == Microsoft.Extensions.Logging.LogLevel.Error
            && ReferenceEquals(entry.Exception, accessException)
            && entry.FormattedMessage.Contains("Unable to buffer the image upload form."));
    }

    [Fact]
    public async Task UploadImage_RejectsMissingOrAmbiguousFile_EmptyFilesCollection_Returns400()
    {
        FormFileCollection files = new FormFileCollection();
        FormCollection form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>(), files);
        StubFormFeature formFeature = new StubFormFeature { Form = form };
        _httpContext.Features.Set<IFormFeature>(formFeature);

        IActionResult result = await _controller.UploadImage(CancellationToken.None);

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
        ProblemDetails problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("Validation.Failed", problem.Extensions["code"]?.ToString());
        Assert.Empty(_sender.Requests);
    }

    [Fact]
    public async Task UploadImage_RejectsMissingOrAmbiguousFile_MultipleFiles_Returns400()
    {
        StubFormFile file1 = new StubFormFile { Name = "file", Length = 100 };
        StubFormFile file2 = new StubFormFile { Name = "file2", Length = 200 };
        FormFileCollection files = new FormFileCollection { file1, file2 };
        FormCollection form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>(), files);
        StubFormFeature formFeature = new StubFormFeature { Form = form };
        _httpContext.Features.Set<IFormFeature>(formFeature);

        IActionResult result = await _controller.UploadImage(CancellationToken.None);

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
        ProblemDetails problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("Validation.Failed", problem.Extensions["code"]?.ToString());
        Assert.False(file1.WasOpenReadStreamCalled);
        Assert.False(file2.WasOpenReadStreamCalled);
        Assert.Empty(_sender.Requests);
    }

    [Fact]
    public async Task UploadImage_RejectsMissingOrAmbiguousFile_WrongFormName_Returns400()
    {
        StubFormFile file = new StubFormFile { Name = "wrong_field_name", Length = 500 };
        FormFileCollection files = new FormFileCollection { file };
        FormCollection form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>(), files);
        StubFormFeature formFeature = new StubFormFeature { Form = form };
        _httpContext.Features.Set<IFormFeature>(formFeature);

        IActionResult result = await _controller.UploadImage(CancellationToken.None);

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
        ProblemDetails problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("Validation.Failed", problem.Extensions["code"]?.ToString());
        Assert.False(file.WasOpenReadStreamCalled);
        Assert.Empty(_sender.Requests);
    }

    [Fact]
    public async Task UploadImage_RejectsMissingOrAmbiguousFile_ZeroLengthFile_Returns400()
    {
        StubFormFile file = new StubFormFile { Name = "file", Length = 0 };
        FormFileCollection files = new FormFileCollection { file };
        FormCollection form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>(), files);
        StubFormFeature formFeature = new StubFormFeature { Form = form };
        _httpContext.Features.Set<IFormFeature>(formFeature);

        IActionResult result = await _controller.UploadImage(CancellationToken.None);

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
        ProblemDetails problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("Validation.Failed", problem.Extensions["code"]?.ToString());
        Assert.False(file.WasOpenReadStreamCalled);
        Assert.Empty(_sender.Requests);
    }

    [Fact]
    public async Task UploadImage_EnforcesMetadataSizeBoundary_ExceedingLimit_Returns413()
    {
        StubFormFile file = new StubFormFile { Name = "file", Length = 5_242_881 };
        FormFileCollection files = new FormFileCollection { file };
        FormCollection form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>(), files);
        StubFormFeature formFeature = new StubFormFeature { Form = form };
        _httpContext.Features.Set<IFormFeature>(formFeature);

        IActionResult result = await _controller.UploadImage(CancellationToken.None);

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, objectResult.StatusCode);
        ProblemDetails problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, problem.Status);
        Assert.Equal("Image.TooLarge", problem.Extensions["code"]?.ToString());
        Assert.False(file.WasOpenReadStreamCalled);
        Assert.Empty(_sender.Requests);
    }

    [Fact]
    public async Task UploadImage_EnforcesMetadataSizeBoundary_ExactLimit_ReachesSender()
    {
        StubFormFile file = new StubFormFile { Name = "file", Length = 5_242_880 };
        FormFileCollection files = new FormFileCollection { file };
        FormCollection form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>(), files);
        StubFormFeature formFeature = new StubFormFeature { Form = form };
        _httpContext.Features.Set<IFormFeature>(formFeature);

        Guid imageId = Guid.NewGuid();
        _sender.RespondWith(Result<UploadImageResponse>.Success(new UploadImageResponse(imageId, $"/uploads/{imageId}.jpg")));

        IActionResult result = await _controller.UploadImage(CancellationToken.None);

        CreatedResult createdResult = Assert.IsType<CreatedResult>(result);
        Assert.Equal(StatusCodes.Status201Created, createdResult.StatusCode);
        Assert.Single(_sender.Requests);
        UploadImageCommand command = Assert.IsType<UploadImageCommand>(_sender.Requests[0]);
        Assert.Equal(5_242_880, command.Length);
    }

    [Fact]
    public async Task UploadImage_MapsStreamOpeningFailure_IOException_Returns503AndLogsError()
    {
        IOException openException = new IOException("Failed to read form file stream");
        StubFormFile file = new StubFormFile
        {
            Name = "file",
            Length = 1024,
            OpenReadStreamException = openException
        };
        FormFileCollection files = new FormFileCollection { file };
        FormCollection form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>(), files);
        StubFormFeature formFeature = new StubFormFeature { Form = form };
        _httpContext.Features.Set<IFormFeature>(formFeature);

        IActionResult result = await _controller.UploadImage(CancellationToken.None);

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);
        ProblemDetails problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("Image.StorageUnavailable", problem.Extensions["code"]?.ToString());
        Assert.Empty(_sender.Requests);

        Assert.Contains(_logger.Entries, entry => entry.LogLevel == Microsoft.Extensions.Logging.LogLevel.Error
            && ReferenceEquals(entry.Exception, openException)
            && entry.FormattedMessage.Contains("Unable to open the uploaded image stream."));
    }

    [Fact]
    public async Task UploadImage_MapsStreamOpeningFailure_UnauthorizedAccessException_Returns503AndLogsError()
    {
        UnauthorizedAccessException accessException = new UnauthorizedAccessException("Permission denied on stream");
        StubFormFile file = new StubFormFile
        {
            Name = "file",
            Length = 1024,
            OpenReadStreamException = accessException
        };
        FormFileCollection files = new FormFileCollection { file };
        FormCollection form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>(), files);
        StubFormFeature formFeature = new StubFormFeature { Form = form };
        _httpContext.Features.Set<IFormFeature>(formFeature);

        IActionResult result = await _controller.UploadImage(CancellationToken.None);

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);
        ProblemDetails problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("Image.StorageUnavailable", problem.Extensions["code"]?.ToString());
        Assert.Empty(_sender.Requests);

        Assert.Contains(_logger.Entries, entry => entry.LogLevel == Microsoft.Extensions.Logging.LogLevel.Error
            && ReferenceEquals(entry.Exception, accessException)
            && entry.FormattedMessage.Contains("Unable to open the uploaded image stream."));
    }

    [Fact]
    public async Task UploadImage_ForwardsActualStreamAndMetadata_Returns201Created()
    {
        using TrackingStream trackingStream = new TrackingStream();
        StubFormFile file = new StubFormFile
        {
            Name = "file",
            FileName = "avatar.png",
            ContentType = "image/png",
            Length = 4096,
            OpenReadStreamHandler = () => trackingStream
        };
        FormFileCollection files = new FormFileCollection { file };
        FormCollection form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>(), files);
        StubFormFeature formFeature = new StubFormFeature { Form = form };
        _httpContext.Features.Set<IFormFeature>(formFeature);

        Guid imageId = Guid.NewGuid();
        string expectedUrl = $"/uploads/{imageId}.png";
        UploadImageResponse responsePayload = new UploadImageResponse(imageId, expectedUrl);

        using CancellationTokenSource cts = new CancellationTokenSource();
        _sender.RespondWith(Result<UploadImageResponse>.Success(responsePayload));

        IActionResult result = await _controller.UploadImage(cts.Token);

        CreatedResult createdResult = Assert.IsType<CreatedResult>(result);
        Assert.Equal(StatusCodes.Status201Created, createdResult.StatusCode);
        Assert.Equal(expectedUrl, createdResult.Location);
        UploadImageResponse returnedValue = Assert.IsType<UploadImageResponse>(createdResult.Value);
        Assert.Equal(imageId, returnedValue.ImageId);
        Assert.Equal(expectedUrl, returnedValue.Url);

        Assert.Single(_sender.Requests);
        UploadImageCommand command = Assert.IsType<UploadImageCommand>(_sender.Requests[0]);
        Assert.Same(trackingStream, command.Content);
        Assert.Equal("avatar.png", command.FileName);
        Assert.Equal("image/png", command.ContentType);
        Assert.Equal(4096, command.Length);
        Assert.Equal(cts.Token, _sender.CancellationTokens[0]);
    }

    [Fact]
    public async Task UploadImage_DisposesStreamAfterSuccessOrBusinessFailure_SuccessDisposesStream()
    {
        TrackingStream trackingStream = new TrackingStream();
        StubFormFile file = new StubFormFile
        {
            Name = "file",
            Length = 100,
            OpenReadStreamHandler = () => trackingStream
        };
        FormFileCollection files = new FormFileCollection { file };
        FormCollection form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>(), files);
        StubFormFeature formFeature = new StubFormFeature { Form = form };
        _httpContext.Features.Set<IFormFeature>(formFeature);

        bool streamUsableDuringSend = false;
        _sender.RespondWith(() =>
        {
            UploadImageCommand cmd = (UploadImageCommand)_sender.Requests[0];
            streamUsableDuringSend = !((TrackingStream)cmd.Content).IsDisposed;
            return Result<UploadImageResponse>.Success(new UploadImageResponse(Guid.NewGuid(), "/uploads/test.jpg"));
        });

        IActionResult result = await _controller.UploadImage(CancellationToken.None);

        Assert.IsType<CreatedResult>(result);
        Assert.True(streamUsableDuringSend);
        Assert.True(trackingStream.IsDisposed);
    }

    [Fact]
    public async Task UploadImage_DisposesStreamAfterSuccessOrBusinessFailure_BusinessFailureDisposesStream()
    {
        TrackingStream trackingStream = new TrackingStream();
        StubFormFile file = new StubFormFile
        {
            Name = "file",
            Length = 100,
            OpenReadStreamHandler = () => trackingStream
        };
        FormFileCollection files = new FormFileCollection { file };
        FormCollection form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>(), files);
        StubFormFeature formFeature = new StubFormFeature { Form = form };
        _httpContext.Features.Set<IFormFeature>(formFeature);

        _sender.RespondWith(Result.Failure<UploadImageResponse>(ImageErrors.InvalidImage));

        IActionResult result = await _controller.UploadImage(CancellationToken.None);

        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
        ProblemDetails problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("Image.InvalidImage", problem.Extensions["code"]?.ToString());
        Assert.True(trackingStream.IsDisposed);
    }

    [Fact]
    public async Task UploadImage_DisposesStreamWhenSenderThrowsOrCancels_SenderFaultDisposesStream()
    {
        TrackingStream trackingStream = new TrackingStream();
        StubFormFile file = new StubFormFile
        {
            Name = "file",
            Length = 100,
            OpenReadStreamHandler = () => trackingStream
        };
        FormFileCollection files = new FormFileCollection { file };
        FormCollection form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>(), files);
        StubFormFeature formFeature = new StubFormFeature { Form = form };
        _httpContext.Features.Set<IFormFeature>(formFeature);

        _sender.RespondWithException(new InvalidOperationException("Sender processing failed abruptly."));

        InvalidOperationException thrown = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _controller.UploadImage(CancellationToken.None));

        Assert.Equal("Sender processing failed abruptly.", thrown.Message);
        Assert.True(trackingStream.IsDisposed);
    }

    [Fact]
    public async Task UploadImage_DisposesStreamWhenSenderThrowsOrCancels_SenderCancellationDisposesStream()
    {
        TrackingStream trackingStream = new TrackingStream();
        StubFormFile file = new StubFormFile
        {
            Name = "file",
            Length = 100,
            OpenReadStreamHandler = () => trackingStream
        };
        FormFileCollection files = new FormFileCollection { file };
        FormCollection form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>(), files);
        StubFormFeature formFeature = new StubFormFeature { Form = form };
        _httpContext.Features.Set<IFormFeature>(formFeature);

        using CancellationTokenSource cts = new CancellationTokenSource();
        cts.Cancel();

        _sender.RespondWithException(new OperationCanceledException(cts.Token));

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _controller.UploadImage(cts.Token));

        Assert.True(trackingStream.IsDisposed);
    }

    [Fact]
    public async Task UploadImage_PropagatesFormCancellation_DoesNotCatchOperationCanceledException()
    {
        using CancellationTokenSource cts = new CancellationTokenSource();
        cts.Cancel();

        StubFormFeature formFeature = new StubFormFeature
        {
            ExceptionToThrowOnRead = new OperationCanceledException(cts.Token)
        };
        _httpContext.Features.Set<IFormFeature>(formFeature);

        OperationCanceledException thrown = await Assert.ThrowsAsync<OperationCanceledException>(
            () => _controller.UploadImage(cts.Token));

        Assert.Equal(cts.Token, thrown.CancellationToken);
        Assert.True(formFeature.WasReadFormAsyncCalled);
        Assert.Equal(cts.Token, formFeature.CapturedCancellationToken);
        Assert.Empty(_sender.Requests);
    }
}
