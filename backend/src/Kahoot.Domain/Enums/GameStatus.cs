namespace Kahoot.Domain.Enums;

public enum GameStatus
{
    // Phase 1: Created - Game created, deep quiz snapshot captured under RepeatableRead, PIN allocated
    Created = 1,

    // Phase 2: Lobby - Host socket connected, PIN discovery active, players joining up to 500-seat limit
    Lobby = 2,

    // Phase 3: QuestionActive - Question timer running, real-time answer submissions accepted
    QuestionActive = 3,

    // Phase 4: QuestionResults - Countdown expired or all eligible answers submitted; correct choice revealed and stats computed
    QuestionResults = 4,

    // Phase 5: Leaderboard - Cumulative ranked scores broadcast to host and player clients
    Leaderboard = 5,

    // Phase 6: Finished - Terminal state; final podium announced, PIN released, and reporting preserved
    Finished = 6
}
