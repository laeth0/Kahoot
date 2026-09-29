namespace Kahoot.Application.Features.Games.StartGame;

using Kahoot.Application.Common.Messaging;

public sealed record StartGameCommand(
    Guid GameId,
    Guid CommandId,
    long ExpectedStateVersion) : ICommand<StartGameResponse>;
