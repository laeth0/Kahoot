namespace Kahoot.Application.Features.Images.UploadImage;

using Kahoot.Application.Common.Exceptions;
using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Persistence;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Auth;
using Kahoot.Domain.Entities;
using Microsoft.EntityFrameworkCore;
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
        // Tenant Isolation - Authenticates host session before accepting image upload
        if (!_currentUser.UserId.HasValue)
        {
            return Result.Failure<UploadImageResponse>(AuthErrors.Unauthorized);
        }

        Guid hostAccountId = _currentUser.UserId.Value;

        // Stream Validation - Validates payload stream existence and non-zero length
        if (request.Length <= 0 || request.Content == Stream.Null)
        {
            return Result.Failure<UploadImageResponse>(ImageErrors.MissingFile);
        }

        // Payload Boundary Guard (IMG-BOUND-001) - Enforces 5 MiB ceiling to prevent storage exhaustion
        if (request.Length > MaxFileSizeBytes)
        {
            return Result.Failure<UploadImageResponse>(ImageErrors.TooLarge);
        }

        // Image Sanitization Pipeline (IMG-UPL-003) - Sniffs magic bytes, strips EXIF, re-encodes raw pixels, eliminates polyglots, and writes to durable storage
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

        // Question-Scoped Image Record (IMG-MODEL-001) - Initializes record with UnreferencedSince = NOW() for 7-day orphan reclamation
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

        // Dual-Write Failure Compensation (IMG-RISK-001, IMG-RISK-002) - Unlinks persisted file if database rejects metadata row
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is DbUpdateException or
                                          UniqueConstraintViolationException or
                                          ForeignKeyConstraintViolationException)
        {
            _logger.LogError(
                exception,
                "Database rejected QuestionImage {ImageId}; compensating file {StoragePath}.",
                sanitized.ImageId,
                sanitized.StoragePath);

            _imageStorageService.CompensateFile(sanitized.StoragePath);
            throw;
        }
        catch (Exception exception)
        {
            // Orphan File Reconciliation Retention (IMG-RISK-001) - Retains file for background reconciliation sweep when commit outcome is uncertain
            _logger.LogError(
                exception,
                "QuestionImage {ImageId} commit outcome is uncertain; retaining file {StoragePath} for reconciliation.",
                sanitized.ImageId,
                sanitized.StoragePath);
            throw;
        }

        // Immutable Public Delivery Contract (IMG-PUB-001) - Returns opaque imageId and immutable URL for question attachment
        UploadImageResponse response = new UploadImageResponse(
            sanitized.ImageId,
            sanitized.StoragePath);

        return Result.Success(response);
    }
}
