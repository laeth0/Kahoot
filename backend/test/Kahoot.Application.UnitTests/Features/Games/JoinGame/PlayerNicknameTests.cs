using Kahoot.Application.Features.Games.JoinGame;
using Xunit;

namespace Kahoot.Application.UnitTests.Features.Games.JoinGame;

public sealed class PlayerNicknameTests
{
    [Fact]
    public void TryNormalize_WithValidNickname_ReturnsTrueWithTrimmedAndUpperNfkc()
    {
        bool success = PlayerNickname.TryNormalize("  PlayerOne  ", out string display, out string normalized);

        Assert.True(success);
        Assert.Equal("PlayerOne", display);
        Assert.Equal("PLAYERONE", normalized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("   \t  ")]
    public void TryNormalize_WhenNullOrWhitespace_ReturnsFalse(string? rawNickname)
    {
        bool success = PlayerNickname.TryNormalize(rawNickname, out string display, out string normalized);

        Assert.False(success);
        Assert.Empty(normalized);
    }

    [Fact]
    public void TryNormalize_WhenSingleCharacter_ReturnsFalseDueToMinimumLength()
    {
        bool success = PlayerNickname.TryNormalize("A", out _, out _);

        Assert.False(success);
    }

    [Fact]
    public void TryNormalize_WhenTwoScalars_ReturnsTrue()
    {
        bool success = PlayerNickname.TryNormalize("AB", out string display, out string normalized);

        Assert.True(success);
        Assert.Equal("AB", display);
        Assert.Equal("AB", normalized);
    }

    [Fact]
    public void TryNormalize_WhenThirtyScalars_ReturnsTrue()
    {
        string thirtyChars = new('A', 30);

        bool success = PlayerNickname.TryNormalize(thirtyChars, out string display, out string normalized);

        Assert.True(success);
        Assert.Equal(thirtyChars, display);
        Assert.Equal(thirtyChars, normalized);
    }

    [Fact]
    public void TryNormalize_WhenThirtyOneScalars_ReturnsFalse()
    {
        string thirtyOneChars = new('A', 31);

        bool success = PlayerNickname.TryNormalize(thirtyOneChars, out _, out _);

        Assert.False(success);
    }

    [Fact]
    public void TryNormalize_WithEmojiScalars_CountsScalarsCorrectly()
    {
        // 2 emojis = 2 Unicode scalar values, each encoded as a surrogate pair (4 UTF-16 chars total)
        string emojiNick = "😀🎉";

        bool success = PlayerNickname.TryNormalize(emojiNick, out string display, out string normalized);

        Assert.True(success);
        Assert.Equal("😀🎉", display);
        Assert.Equal("😀🎉", normalized);
    }

    [Fact]
    public void TryNormalize_WithMalformedUtf16UnpairedSurrogate_ReturnsFalse()
    {
        // High surrogate without low surrogate
        string malformed = "Abc\uD800Def";

        bool success = PlayerNickname.TryNormalize(malformed, out _, out _);

        Assert.False(success);
    }

    [Theory]
    [InlineData("Player\u0000One")] // Null control char
    [InlineData("Player\tOne")]     // Tab control char
    [InlineData("Player\r\nOne")]   // CR/LF control chars
    [InlineData("Player\u200BOne")] // Zero-width space format char
    [InlineData("Player\u200EOne")] // LTR mark format char
    public void TryNormalize_WithControlOrFormatCharacters_ReturnsFalse(string invalidNick)
    {
        bool success = PlayerNickname.TryNormalize(invalidNick, out _, out _);

        Assert.False(success);
    }

    [Fact]
    public void TryNormalize_NormalizesLigaturesAndCompatibilityCharacters()
    {
        // 'ﬁ' expands to 'fi' and uppercases to 'FI'
        string ligatureNick = "ﬁrst";

        bool success = PlayerNickname.TryNormalize(ligatureNick, out string display, out string normalized);

        Assert.True(success);
        Assert.Equal("ﬁrst", display);
        Assert.Equal("FIRST", normalized);
    }
}
