namespace Kahoot.Domain.Games;

public static class GameStatusTransitions
{
    private static readonly IReadOnlyDictionary<GameStatus, GameStatus[]> AllowedTargets =
        new Dictionary<GameStatus, GameStatus[]>
        {
            [GameStatus.Created] = [GameStatus.Lobby],
            [GameStatus.Lobby] = [GameStatus.QuestionActive, GameStatus.Finished],
            [GameStatus.QuestionActive] = [GameStatus.QuestionResults, GameStatus.Finished],
            [GameStatus.QuestionResults] = [GameStatus.Leaderboard, GameStatus.Finished],
            [GameStatus.Leaderboard] = [GameStatus.QuestionActive, GameStatus.Finished],
            [GameStatus.Finished] = []
        };

    public static bool IsAllowed(GameStatus from, GameStatus to) =>
        AllowedTargets.TryGetValue(from, out var targets) && Array.IndexOf(targets, to) >= 0;

    public static IReadOnlyCollection<GameStatus> AllowedFrom(GameStatus from) =>
        AllowedTargets.TryGetValue(from, out var targets) ? targets : [];
}
