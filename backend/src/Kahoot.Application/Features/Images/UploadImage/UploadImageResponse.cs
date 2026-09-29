namespace Kahoot.Application.Features.Images.UploadImage;

public sealed record UploadImageResponse(
    // Question Image Identity - Opaque UUIDv4 primary key used by host to attach image to question (IMG-ATT-001)
    Guid ImageId,
    // Immutable Delivery URL - Public cache-friendly URL (/uploads/{guid}.{ext}) for browser asset retrieval (IMG-PUB-001)
    string Url);
