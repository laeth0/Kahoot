using System.ComponentModel.DataAnnotations;

namespace Kahoot.Application.Common.Storage;

public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    [Required]
    public string RootPath { get; set; } = "uploads";

    [Required]
    public string PublicBasePath { get; set; } = "/uploads";

    [Range(1, 52_428_800)]
    public long MaxSizeBytes { get; set; } = 5_242_880;

    [Required]
    [MinLength(1)]
    public string[] AllowedContentTypes { get; set; } = ["image/jpeg", "image/png", "image/webp", "image/gif"];

    public string NormalizedPublicBasePath => "/" + PublicBasePath.Trim('/');

    public string ResolveRootPath(string contentRootPath) =>
        Path.IsPathRooted(RootPath) ? RootPath : Path.Combine(contentRootPath, RootPath);
}
