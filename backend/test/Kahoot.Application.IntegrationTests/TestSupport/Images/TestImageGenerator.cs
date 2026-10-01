namespace Kahoot.Application.IntegrationTests.TestSupport.Images;

using System.IO;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

public static class TestImageGenerator
{
    public static async Task<MemoryStream> CreateValidPngStreamAsync(int width = 16, int height = 16)
    {
        using Image<Rgba32> image = new(width, height);
        MemoryStream stream = new();
        await image.SaveAsPngAsync(stream);
        stream.Position = 0;
        return stream;
    }

    public static async Task<MemoryStream> CreateValidJpegStreamAsync(int width = 16, int height = 16)
    {
        using Image<Rgba32> image = new(width, height);
        MemoryStream stream = new();
        await image.SaveAsJpegAsync(stream);
        stream.Position = 0;
        return stream;
    }

    public static async Task<MemoryStream> CreateMetadataBearingJpegStreamAsync(int width = 16, int height = 16, string software = "KahootIntegrationTest")
    {
        using Image<Rgba32> image = new(width, height);
        ExifProfile exif = new();
        exif.SetValue(ExifTag.Software, software);
        image.Metadata.ExifProfile = exif;

        MemoryStream stream = new();
        await image.SaveAsJpegAsync(stream);
        stream.Position = 0;
        return stream;
    }
}
