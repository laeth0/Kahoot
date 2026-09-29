namespace Kahoot.Application.Features.Images.UploadImage;

using Kahoot.Application.Common.Messaging;

public sealed record UploadImageCommand(
    // Payload Stream - Inbound multipart stream to be sanitized and persisted
    Stream Content,
    // Original File Name - Client-supplied name used for extension sniffing before random UUID assignment (IMG-SEC-002)
    string FileName,
    // Declared MIME Type - Content-Type header checked against allowlist (JPEG, PNG, WebP)
    string? ContentType,
    // Payload Byte Length - Raw upload length enforced against 5 MiB ceiling (IMG-BOUND-001)
    long Length) : ICommand<UploadImageResponse>;
