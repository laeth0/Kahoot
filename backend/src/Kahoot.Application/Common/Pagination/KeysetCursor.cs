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

    // Zero-Allocation Keyset Encoding - Formats UTC ticks and GUID directly into stack memory before Base64Url serialization
    public static string Encode(DateTimeOffset createdAt, Guid id)
    {
        // Stack Memory Buffer - Preallocates fixed-size 64-byte stack buffer to avoid heap allocations
        Span<byte> utf8Buffer = stackalloc byte[64];
        if (Utf8.TryWrite(utf8Buffer, $"{createdAt.UtcTicks}:{id:N}", out int bytesWritten))
        {
            return Base64Url.EncodeToString(utf8Buffer[..bytesWritten]);
        }

        // Fallback Heap Allocation - Safeguard path in rare event stack buffer is insufficient
        byte[] fallback = Encoding.UTF8.GetBytes($"{createdAt.UtcTicks}:{id:N}");
        return Base64Url.EncodeToString(fallback);
    }

    // High-Performance Cursor Parsing - Validates and decodes opaque Base64Url token with zero heap garbage
    public static bool TryDecode(string? cursor, out KeysetCursor? result)
    {
        // Null or Whitespace Guard - Fast-exits on empty pagination cursor inputs
        if (string.IsNullOrWhiteSpace(cursor))
        {
            result = null;
            return false;
        }

        // Stack Memory Decoding Buffer - Decodes Base64Url into stack memory
        Span<byte> decodedBytes = stackalloc byte[64];
        OperationStatus status = Base64Url.DecodeFromChars(cursor.AsSpan(), decodedBytes, out int charsConsumed, out int bytesWritten);
        if (status != OperationStatus.Done || charsConsumed != cursor.Length)
        {
            result = null;
            return false;
        }

        // Delimiter Position Validation - Locates separator colon separating ticks and UUID
        int colonIndex = decodedBytes[..bytesWritten].IndexOf((byte)':');
        if (colonIndex <= 0)
        {
            result = null;
            return false;
        }

        ReadOnlySpan<byte> ticksBytes = decodedBytes[..colonIndex];
        ReadOnlySpan<byte> idBytes = decodedBytes[(colonIndex + 1)..bytesWritten];

        // Zero-Allocation Primitive Parsing - Verifies ticks range and parse GUID in standard 'N' format
        if (Utf8Parser.TryParse(ticksBytes, out long ticks, out int ticksConsumed) &&
            ticksConsumed == ticksBytes.Length &&
            ticks >= DateTimeOffset.MinValue.UtcTicks &&
            ticks <= DateTimeOffset.MaxValue.UtcTicks &&
            Utf8Parser.TryParse(idBytes, out Guid id, out int idConsumed, 'N') &&
            idConsumed == idBytes.Length)
        {
            result = new KeysetCursor(new DateTimeOffset(ticks, TimeSpan.Zero), id);
            return true;
        }

        result = null;
        return false;
    }
}
