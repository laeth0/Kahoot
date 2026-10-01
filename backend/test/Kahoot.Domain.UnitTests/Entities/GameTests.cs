using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Xunit;

namespace Kahoot.Domain.UnitTests.Entities;

public sealed class GameTests
{
    [Fact]
    public void NewGame_StartsInCreatedState()
    {
        Game game = new() { Title = "Test game" };

        Assert.Equal(GameStatus.Created, game.Status);
    }

    [Fact]
    public void NewGame_StartsWithInitialStateVersion()
    {
        Game game = new() { Title = "Test game" };

        Assert.Equal(1L, game.StateVersion);
    }

    [Fact]
    public void NewGame_StartsSeatNumberingAtOne()
    {
        Game game = new() { Title = "Test game" };

        Assert.Equal(1, game.NextSeatNumber);
    }
}
