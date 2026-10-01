using Kahoot.Application.Features.Auth;
using Xunit;

namespace Kahoot.Application.UnitTests.Features.Auth;

public sealed class UsernameNormalizationTests
{
    [Fact]
    public void GetDisplayUsername_StripsLeadingAndTrailingSpacesOnly()
    {
        const string rawUsername = "  John Doe  ";

        string displayUsername = UsernameNormalization.GetDisplayUsername(rawUsername);

        Assert.Equal("John Doe", displayUsername);
    }

    [Fact]
    public void GetDisplayUsername_PreservesOriginalCasing()
    {
        const string rawUsername = "AliceWonderland";

        string displayUsername = UsernameNormalization.GetDisplayUsername(rawUsername);

        Assert.Equal("AliceWonderland", displayUsername);
    }

    [Fact]
    public void GetNormalizedUsername_NormalizesNfkcAndConvertsToUppercaseInvariant()
    {
        const string displayUsername = "john_doe";

        string normalized = UsernameNormalization.GetNormalizedUsername(displayUsername);

        Assert.Equal("JOHN_DOE", normalized);
    }

    [Fact]
    public void GetNormalizedUsername_DecomposesCompatibilityCharacters()
    {
        // Ligature 'ﬁ' (U+FB01) decomposes to 'fi' in NFKC and folds to "FI"
        const string displayWithLigature = "ﬁle";

        string normalized = UsernameNormalization.GetNormalizedUsername(displayWithLigature);

        Assert.Equal("FILE", normalized);
    }

    [Fact]
    public void GetNormalizedUsername_NormalizesFullWidthAsciiCharacters()
    {
        // Fullwidth 'Ａｌｉｃｅ' (U+FF21 U+FF4C U+FF49 U+FF43 U+FF45) normalizes to "ALICE"
        const string fullWidthUsername = "\uFF21\uFF4C\uFF49\uFF43\uFF45";

        string normalized = UsernameNormalization.GetNormalizedUsername(fullWidthUsername);

        Assert.Equal("ALICE", normalized);
    }
}
