using System.Buffers;
using System.Buffers.Text;
using System.Text;
using System.Text.Unicode;

namespace Kahoot.Application.Common.Pagination;

public sealed record KeysetCursor
{
    public DateTimeOffset CreatedAt { get; init; }

    public Guid Id { get; init; }

    public KeysetCursor(DateTimeOffset createdAt, Guid id)
    {
        CreatedAt = createdAt;
        Id = id;
    }

    public static string Encode(DateTimeOffset createdAt, Guid id)
    {
        Span<byte> utf8Buffer = stackalloc byte[64];
        if (Utf8.TryWrite(utf8Buffer, $"{createdAt.UtcTicks}:{id:N}", out int bytesWritten))
        {
            return Base64Url.EncodeToString(utf8Buffer[..bytesWritten]);
        }

        byte[] fallback = Encoding.UTF8.GetBytes($"{createdAt.UtcTicks}:{id:N}");
        return Base64Url.EncodeToString(fallback);
    }

    public static bool TryDecode(string? cursor, out KeysetCursor? result)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            result = null;
            return false;
        }

        Span<byte> decodedBytes = stackalloc byte[64];
        OperationStatus status = Base64Url.DecodeFromChars(cursor.AsSpan(), decodedBytes, out _, out int bytesWritten);
        if (status != OperationStatus.Done)
        {
            result = null;
            return false;
        }

        int colonIndex = decodedBytes[..bytesWritten].IndexOf((byte)':');
        if (colonIndex <= 0)
        {
            result = null;
            return false;
        }

        if (Utf8Parser.TryParse(decodedBytes[..colonIndex], out long ticks, out _) &&
            Utf8Parser.TryParse(decodedBytes[(colonIndex + 1)..bytesWritten], out Guid id, out _, 'N'))
        {
            result = new KeysetCursor(new DateTimeOffset(ticks, TimeSpan.Zero), id);
            return true;
        }

        result = null;
        return false;
    }
}
