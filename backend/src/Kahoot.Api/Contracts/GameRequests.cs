namespace Kahoot.Api.Contracts;

public sealed record CreateGameRequest(Guid QuizId);

public sealed record JoinGameRequest(string Pin, string Nickname);
