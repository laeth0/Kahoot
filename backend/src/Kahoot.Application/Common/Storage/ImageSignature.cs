namespace Kahoot.Application.Common.Storage;

internal static class ImageSignature
{
    private const int HeaderLength = 12;

    private static ReadOnlySpan<byte> Png => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    public static bool Matches(Stream stream, string contentType)
    {
        Span<byte> header = stackalloc byte[HeaderLength];
        int read = stream.ReadAtLeast(header, HeaderLength, throwOnEndOfStream: false);
        stream.Position = 0;

        if (read < HeaderLength)
        {
            return false;
        }

        return contentType switch
        {
            "image/jpeg" => header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            "image/png" => header[..8].SequenceEqual(Png),
            "image/gif" => header[..4].SequenceEqual("GIF8"u8),
            "image/webp" => header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8),
            _ => false
        };
    }
}
