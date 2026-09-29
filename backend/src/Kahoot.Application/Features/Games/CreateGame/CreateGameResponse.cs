namespace Kahoot.Application.Features.Games.CreateGame;

public sealed record CreateGameResponse(
    Guid GameId,
    string Pin,
    string JoinUrl,
    string Title,
    string Status,
    long StateVersion,
    int TotalQuestions,
    DateTimeOffset CreatedAt);
