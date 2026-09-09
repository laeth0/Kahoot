using Kahoot.Domain.Games;

namespace Kahoot.Application.Games;

public enum GameTransition
{
    OpenLobby,
    StartFirstQuestion,
    RevealQuestionResults,
    ShowLeaderboard,
    AdvanceToNextQuestion,
    EndGame
}

public static class GameStateMachine
{
    private sealed record TransitionRule(GameStatus[] From, GameStatus To);

    private static readonly Dictionary<GameTransition, TransitionRule> Rules =
        new()
        {
            [GameTransition.OpenLobby] = new([GameStatus.Created], GameStatus.Lobby),
            [GameTransition.StartFirstQuestion] = new([GameStatus.Lobby], GameStatus.QuestionActive),
            [GameTransition.RevealQuestionResults] = new([GameStatus.QuestionActive], GameStatus.QuestionResults),
            [GameTransition.ShowLeaderboard] = new([GameStatus.QuestionResults], GameStatus.Leaderboard),
            [GameTransition.AdvanceToNextQuestion] = new([GameStatus.Leaderboard], GameStatus.QuestionActive),
            [GameTransition.EndGame] = new(
                [GameStatus.Lobby, GameStatus.QuestionActive, GameStatus.QuestionResults, GameStatus.Leaderboard],
                GameStatus.Finished)
        };

    public static bool CanFire(GameStatus current, GameTransition transition) =>
        Rules.TryGetValue(transition, out TransitionRule? rule) && Array.IndexOf(rule.From, current) >= 0;

    public static GameStatus TargetOf(GameTransition transition) => Rules[transition].To;
}
