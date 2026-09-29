namespace Kahoot.Application.Features.Games.JoinGame;

using System.Buffers;
using System.Globalization;
using System.Text;

internal static class PlayerNickname
{
    public static bool TryNormalize(string? nickname, out string display, out string normalized)
    {
        // Trim Whitespace - Cleans boundary whitespace before length or character verification
        display = nickname?.Trim() ?? string.Empty;
        normalized = string.Empty;
        if (!IsValid(display))
        {
            return false;
        }

        // Unicode NFKC Normalization & Case Folding - Eliminates visual homoglyphs and ligature exploits before uniqueness checks
        normalized = display.Normalize(NormalizationForm.FormKC).ToUpperInvariant();
        return IsValid(normalized);
    }

    private static bool IsValid(string value)
    {
        int scalars = 0;
        // Zero-Allocation Span Traversal - Operates directly over UTF-16 characters without string allocations
        ReadOnlySpan<char> remaining = value.AsSpan();
        while (!remaining.IsEmpty)
        {
            // UTF-16 Rune Decoding - Safely parses complete Unicode scalar values without splitting surrogate pairs
            if (Rune.DecodeFromUtf16(remaining, out Rune rune, out int consumed) != OperationStatus.Done)
            {
                return false;
            }

            // Input Sanitization - Rejects invisible control codes and bidi format characters that disrupt UI rendering
            UnicodeCategory category = Rune.GetUnicodeCategory(rune);
            if (category is UnicodeCategory.Control or UnicodeCategory.Format)
            {
                return false;
            }

            scalars++;
            // Bounded Scalar Capacity - Limits nickname length by Unicode scalar count rather than char count (max 30)
            if (scalars > 30)
            {
                return false;
            }

            remaining = remaining[consumed..];
        }

        // Minimum Length Constraint - Requires at least 2 valid Unicode scalar characters
        return scalars >= 2;
    }
}
