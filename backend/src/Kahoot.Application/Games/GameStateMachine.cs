using Kahoot.Domain.Games;

namespace Kahoot.Application.Games;

public static class GameStateMachine
{
    private static readonly IReadOnlyDictionary<GameStatus, GameStatus[]> AllowedTransitions =
        new Dictionary<GameStatus, GameStatus[]>
        {
            [GameStatus.Created] = [GameStatus.Lobby],
            [GameStatus.Lobby] = [GameStatus.QuestionActive, GameStatus.Finished],
            [GameStatus.QuestionActive] = [GameStatus.QuestionResults, GameStatus.Finished],
            [GameStatus.QuestionResults] = [GameStatus.Leaderboard, GameStatus.Finished],
            [GameStatus.Leaderboard] = [GameStatus.QuestionActive, GameStatus.Finished],
            [GameStatus.Finished] = []
        };

    public static bool CanTransition(GameStatus from, GameStatus to) =>
        AllowedTransitions.TryGetValue(from, out GameStatus[]? targets) && Array.IndexOf(targets, to) >= 0;
}
