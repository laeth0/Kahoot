namespace Kahoot.Application.Features.Games.JoinGame;

public sealed record JoinGameResponse(
    Guid ParticipantId,
    string PlayerSessionToken,
    Guid GameId,
    string Nickname,
    string Title,
    int SeatNumber);
