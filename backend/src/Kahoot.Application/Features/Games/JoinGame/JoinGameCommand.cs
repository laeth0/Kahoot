namespace Kahoot.Application.Features.Games.JoinGame;

using Kahoot.Application.Common.Messaging;

public sealed record JoinGameCommand(
    string Pin,
    string Nickname,
    // Client Idempotency Identifier - UUIDv4 ensuring at-most-once join handling across network retries
    Guid JoinOperationId,
    // Rate Limiting Dimension - Remote client IP paired with game PIN to throttle distributed credential stuffing
    string IpAddress = "") : ICommand<JoinGameResponse>;
