namespace Kahoot.Application.Features.Images.UploadImage;

public sealed record UploadImageResponse(
    Guid ImageId,
    string Url);
