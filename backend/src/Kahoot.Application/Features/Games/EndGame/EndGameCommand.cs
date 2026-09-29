namespace Kahoot.Application.Features.Games.EndGame;

using Kahoot.Application.Common.Messaging;

public sealed record EndGameCommand(
    Guid GameId,
    Guid CommandId,
    long ExpectedStateVersion) : ICommand<EndGameResponse>;
