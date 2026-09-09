using Kahoot.Domain.Common;

namespace Kahoot.Domain.Games;

public sealed class InvalidGameStatusTransitionException : DomainException
{
    public InvalidGameStatusTransitionException(GameStatus from, GameStatus to)
        : base($"Game status transition from '{from}' to '{to}' is not allowed.")
    {
        From = from;
        To = to;
    }

    public GameStatus From { get; }

    public GameStatus To { get; }
}
