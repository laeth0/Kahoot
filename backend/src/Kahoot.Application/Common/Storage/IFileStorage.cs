namespace Kahoot.Application.Common.Storage;

public interface IFileStorage
{
    Task<string> SaveAsync(Stream content, string fileExtension, CancellationToken cancellationToken);
    bool Exists(string publicPath);
}
