using Kahoot.Domain.Enums;
using Xunit;

namespace Kahoot.Domain.UnitTests.Enums;

public sealed class UserRoleTests
{
    [Fact]
    public void DefinedNames_MatchSupportedAccountRoles()
    {
        string[] expectedNames = ["Host", "SystemAdmin"];

        Assert.Equal(
            expectedNames.Order(StringComparer.Ordinal),
            Enum.GetNames<UserRole>().Order(StringComparer.Ordinal));
    }

    [Fact]
    public void DefinedValues_AreDistinct()
    {
        Assert.Distinct(Enum.GetValues<UserRole>());
    }
}
