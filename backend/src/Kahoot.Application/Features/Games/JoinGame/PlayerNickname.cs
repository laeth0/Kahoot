namespace Kahoot.Application.Features.Games.JoinGame;

using System.Buffers;
using System.Globalization;
using System.Text;

internal static class PlayerNickname
{
    public static bool TryNormalize(string? nickname, out string display, out string normalized)
    {
        display = nickname?.Trim() ?? string.Empty;
        normalized = string.Empty;
        if (!IsValid(display))
        {
            return false;
        }

        normalized = display.Normalize(NormalizationForm.FormKC).ToUpperInvariant();
        return IsValid(normalized);
    }

    private static bool IsValid(string value)
    {
        int scalars = 0;
        ReadOnlySpan<char> remaining = value.AsSpan();
        while (!remaining.IsEmpty)
        {
            if (Rune.DecodeFromUtf16(remaining, out Rune rune, out int consumed) != OperationStatus.Done)
            {
                return false;
            }

            UnicodeCategory category = Rune.GetUnicodeCategory(rune);
            if (category is UnicodeCategory.Control or UnicodeCategory.Format)
            {
                return false;
            }

            scalars++;
            if (scalars > 30)
            {
                return false;
            }

            remaining = remaining[consumed..];
        }

        return scalars >= 2;
    }
}
