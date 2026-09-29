namespace Kahoot.Application.Features.Games.JoinGame;

public sealed record JoinGameResponse(
    Guid ParticipantId,
    // Ephemeral Player Token - Signed HMAC-SHA256 token granting authenticated real-time hub access without an account
    string PlayerSessionToken,
    Guid GameId,
    string Nickname,
    string Title,
    // Monotonic Seat Allocation - Zero-fragmentation monotonic sequence number assigned under table row lock
    int SeatNumber);
