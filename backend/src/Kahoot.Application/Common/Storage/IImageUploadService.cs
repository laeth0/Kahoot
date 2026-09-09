using Kahoot.Domain.Common;

namespace Kahoot.Application.Common.Storage;

public interface IImageUploadService
{
    Task<Result<string>> UploadImageAsync(
        Stream content,
        string? contentType,
        long length,
        CancellationToken cancellationToken);
}
