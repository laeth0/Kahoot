namespace Kahoot.Application.Features.Images.UploadImage;

using Kahoot.Application.Common.Messaging;

public sealed record UploadImageCommand(
    Stream Content,
    string FileName,
    string? ContentType,
    long Length) : ICommand<UploadImageResponse>;
