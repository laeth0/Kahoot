using Kahoot.Domain.Enums;
using Xunit;

namespace Kahoot.Domain.UnitTests.Enums;

public sealed class GameStatusTests
{
    [Fact]
    public void DefinedNames_MatchCanonicalLifecycleStates()
    {
        string[] expectedNames =
        [
            "Created",
            "Lobby",
            "QuestionActive",
            "QuestionResults",
            "Leaderboard",
            "Finished"
        ];

        Assert.Equal(
            expectedNames.Order(StringComparer.Ordinal),
            Enum.GetNames<GameStatus>().Order(StringComparer.Ordinal));
    }

    [Fact]
    public void DefinedValues_AreDistinct()
    {
        Assert.Distinct(Enum.GetValues<GameStatus>());
    }
}
