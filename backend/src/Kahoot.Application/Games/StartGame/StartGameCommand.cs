using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Games.Common;

namespace Kahoot.Application.Games.StartGame;

public sealed record StartGameCommand(Guid GameId) : ICommand<QuestionStartedResponse>;
