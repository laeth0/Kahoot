using Kahoot.Domain.Common;

namespace Kahoot.Domain.Games;

public static class GameErrors
{
    public static Error InvalidStatusTransition(GameStatus from, GameStatus to) =>
        new("Game.InvalidStatusTransition", $"A game in state '{from}' cannot transition to '{to}'.");
}
