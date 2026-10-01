using Kahoot.Domain.Enums;
using Xunit;

namespace Kahoot.Domain.UnitTests.Enums;

public sealed class UserStatusTests
{
    [Fact]
    public void DefinedNames_MatchSupportedAccountStatuses()
    {
        string[] expectedNames = ["Active", "Suspended"];

        Assert.Equal(
            expectedNames.Order(StringComparer.Ordinal),
            Enum.GetNames<UserStatus>().Order(StringComparer.Ordinal));
    }

    [Fact]
    public void DefinedValues_AreDistinct()
    {
        Assert.Distinct(Enum.GetValues<UserStatus>());
    }
}
