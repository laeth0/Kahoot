using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Kahoot.Infrastructure.Storage;

public sealed class LocalFileStorage : IFileStorage, ISingletonService
{
    private readonly string _rootPath;
    private readonly string _publicBasePath;

    public LocalFileStorage(IOptions<FileStorageOptions> options, IHostEnvironment environment)
    {
        FileStorageOptions value = options.Value;
        _rootPath = value.ResolveRootPath(environment.ContentRootPath);
        _publicBasePath = value.NormalizedPublicBasePath;
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> SaveAsync(Stream content, string fileExtension, CancellationToken cancellationToken)
    {
        string fileName = Guid.CreateVersion7().ToString("n") + fileExtension;
        string absolutePath = Path.Combine(_rootPath, fileName);

        await using FileStream target = new(absolutePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(target, cancellationToken);

        return $"{_publicBasePath}/{fileName}";
    }

    public bool Exists(string publicPath)
    {
        if (string.IsNullOrWhiteSpace(publicPath))
        {
            return false;
        }

        string prefix = $"{_publicBasePath}/";
        if (!publicPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string fileName = publicPath[prefix.Length..];
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Contains('/') || fileName.Contains('\\') || fileName.Contains(".."))
        {
            return false;
        }

        string absolutePath = Path.Combine(_rootPath, fileName);
        return File.Exists(absolutePath);
    }
}
