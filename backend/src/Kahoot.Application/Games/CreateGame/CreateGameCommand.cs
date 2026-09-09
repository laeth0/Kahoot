using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Games.Common;

namespace Kahoot.Application.Games.CreateGame;

public sealed record CreateGameCommand(Guid QuizId) : ICommand<CreateGameResponse>;
