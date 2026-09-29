namespace Kahoot.Application.Features.Games.JoinGame;

using Kahoot.Application.Common.Messaging;

public sealed record JoinGameCommand(
    string Pin,
    string Nickname,
    Guid JoinOperationId,
    string IpAddress = "") : ICommand<JoinGameResponse>;
