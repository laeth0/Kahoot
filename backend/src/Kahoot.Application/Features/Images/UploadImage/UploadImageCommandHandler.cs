namespace Kahoot.Application.Features.Images.UploadImage;

using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Persistence;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Auth;
using Kahoot.Domain.Entities;
using Microsoft.Extensions.Logging;

public sealed class UploadImageCommandHandler : ICommandHandler<UploadImageCommand, UploadImageResponse>
{
    private const long MaxFileSizeBytes = 5_242_880;

    private readonly IAppDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IImageStorageService _imageStorageService;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<UploadImageCommandHandler> _logger;

    public UploadImageCommandHandler(
        IAppDbContext dbContext,
        ICurrentUser currentUser,
        IImageStorageService imageStorageService,
        TimeProvider timeProvider,
        ILogger<UploadImageCommandHandler> logger)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _imageStorageService = imageStorageService;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Result<UploadImageResponse>> Handle(
        UploadImageCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Result.Failure<UploadImageResponse>(AuthErrors.Unauthorized);
        }

        Guid hostAccountId = _currentUser.UserId.Value;

        if (request.Length <= 0 || request.Content == Stream.Null)
        {
            return Result.Failure<UploadImageResponse>(ImageErrors.MissingFile);
        }

        if (request.Length > MaxFileSizeBytes)
        {
            return Result.Failure<UploadImageResponse>(ImageErrors.TooLarge);
        }

        Result<SanitizedImageResult> sanitizeResult = await _imageStorageService.SanitizeAndPersistAsync(
            request.Content,
            request.FileName,
            request.ContentType,
            cancellationToken);

        if (!sanitizeResult.IsSuccess)
        {
            return Result.Failure<UploadImageResponse>(sanitizeResult.Error);
        }

        SanitizedImageResult sanitized = sanitizeResult.Value;
        DateTimeOffset utcNow = _timeProvider.GetUtcNow();

        QuestionImage questionImage = new QuestionImage
        {
            Id = sanitized.ImageId,
            HostAccountId = hostAccountId,
            StoragePath = sanitized.StoragePath,
            ContentType = sanitized.ContentType,
            ByteSize = sanitized.ByteSize,
            PixelWidth = sanitized.PixelWidth,
            PixelHeight = sanitized.PixelHeight,
            UnreferencedSince = utcNow,
            CreatedAt = utcNow
        };

        _dbContext.QuestionImages.Add(questionImage);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Database commit failed for QuestionImage {ImageId}. Initiating file compensation for {StoragePath}.",
                sanitized.ImageId,
                sanitized.StoragePath);

            _imageStorageService.CompensateFile(sanitized.StoragePath);
            throw;
        }

        UploadImageResponse response = new UploadImageResponse(
            sanitized.ImageId,
            sanitized.StoragePath);

        return Result.Success(response);
    }
}
