namespace Kahoot.Application.Features.Games.JoinGame;

public sealed record JoinGameRequest(
    string Pin,
    string Nickname,
    Guid JoinOperationId);
