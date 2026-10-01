using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Auth;
using Kahoot.Application.Features.Images;
using Kahoot.Application.Features.Images.UploadImage;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Kahoot.Application.UnitTests.Features.Images;

public sealed class UploadImageCommandHandlerTests
{
    private readonly TestCurrentUser _currentUser = new();
    private readonly TestImageStorageService _storageService = new();
    private readonly TestLogger<UploadImageCommandHandler> _logger = new();

    private UploadImageCommandHandler CreateHandler()
    {
        return new UploadImageCommandHandler(
            dbContext: null!, // DbContext is never reached in pre-persistence guard tests
            currentUser: _currentUser,
            imageStorageService: _storageService,
            timeProvider: TimeProvider.System,
            logger: _logger);
    }

    [Fact]
    public async Task Handle_WhenCallerNotAuthenticated_ReturnsUnauthorizedError()
    {
        _currentUser.UserId = null;
        UploadImageCommandHandler handler = CreateHandler();
        using MemoryStream stream = new(new byte[] { 1, 2, 3 });
        UploadImageCommand command = new(stream, "image.png", "image/png", 3);

        Result<UploadImageResponse> result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(AuthErrors.Unauthorized, result.Error);
    }

    [Fact]
    public async Task Handle_WhenContentStreamIsNullStream_ReturnsMissingFileError()
    {
        _currentUser.UserId = Guid.NewGuid();
        UploadImageCommandHandler handler = CreateHandler();
        UploadImageCommand command = new(Stream.Null, "image.png", "image/png", 100);

        Result<UploadImageResponse> result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ImageErrors.MissingFile, result.Error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Handle_WhenContentLengthZeroOrNegative_ReturnsMissingFileError(long invalidLength)
    {
        _currentUser.UserId = Guid.NewGuid();
        UploadImageCommandHandler handler = CreateHandler();
        using MemoryStream stream = new(new byte[] { 1, 2, 3 });
        UploadImageCommand command = new(stream, "image.png", "image/png", invalidLength);

        Result<UploadImageResponse> result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ImageErrors.MissingFile, result.Error);
    }

    [Fact]
    public async Task Handle_WhenFileExceeds5MiB_ReturnsTooLargeError()
    {
        _currentUser.UserId = Guid.NewGuid();
        UploadImageCommandHandler handler = CreateHandler();
        using MemoryStream stream = new(new byte[] { 1, 2, 3 });
        const long oversized = 5_242_881L; // 5 MiB + 1 byte
        UploadImageCommand command = new(stream, "image.png", "image/png", oversized);

        Result<UploadImageResponse> result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ImageErrors.TooLarge, result.Error);
    }

    [Fact]
    public async Task Handle_WhenStorageSanitizationFails_PropagatesStorageError()
    {
        _currentUser.UserId = Guid.NewGuid();
        _storageService.ResultToReturn = Result.Failure<SanitizedImageResult>(ImageErrors.UnsupportedType);
        UploadImageCommandHandler handler = CreateHandler();
        using MemoryStream stream = new(new byte[] { 1, 2, 3 });
        UploadImageCommand command = new(stream, "document.pdf", "application/pdf", 100);

        Result<UploadImageResponse> result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ImageErrors.UnsupportedType, result.Error);
    }

    [Fact]
    public async Task Handle_WhenStorageSanitizationDetectsCorruptImage_PropagatesInvalidImageError()
    {
        _currentUser.UserId = Guid.NewGuid();
        _storageService.ResultToReturn = Result.Failure<SanitizedImageResult>(ImageErrors.InvalidImage);
        UploadImageCommandHandler handler = CreateHandler();
        using MemoryStream stream = new(new byte[] { 1, 2, 3 });
        UploadImageCommand command = new(stream, "corrupt.png", "image/png", 100);

        Result<UploadImageResponse> result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ImageErrors.InvalidImage, result.Error);
    }

    [Fact]
    public async Task Handle_WhenStorageCapacityExhausted_PropagatesStorageUnavailableError()
    {
        _currentUser.UserId = Guid.NewGuid();
        _storageService.ResultToReturn = Result.Failure<SanitizedImageResult>(ImageErrors.StorageUnavailable);
        UploadImageCommandHandler handler = CreateHandler();
        using MemoryStream stream = new(new byte[] { 1, 2, 3 });
        UploadImageCommand command = new(stream, "image.png", "image/png", 100);

        Result<UploadImageResponse> result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ImageErrors.StorageUnavailable, result.Error);
    }

    private sealed class TestCurrentUser : ICurrentUser
    {
        public Guid? UserId { get; set; } = Guid.NewGuid();
        public string? Role { get; set; } = "Host";
        public bool IsAuthenticated => UserId.HasValue;
    }

    private sealed class TestImageStorageService : IImageStorageService
    {
        public Result<SanitizedImageResult> ResultToReturn { get; set; } =
            Result.Success(new SanitizedImageResult(Guid.NewGuid(), "/images/test.webp", "image/webp", 100, 100, 100));

        public bool IsStorageAvailable() => true;

        public Task<Result<SanitizedImageResult>> SanitizeAndPersistAsync(
            Stream sourceStream,
            string originalFileName,
            string? declaredContentType,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(ResultToReturn);
        }

        public void CompensateFile(string storagePath)
        {
        }

        public string? GetPhysicalFilePath(string storagePath) => null;
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
        }
    }
}
