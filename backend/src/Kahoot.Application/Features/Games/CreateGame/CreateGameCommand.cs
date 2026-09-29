namespace Kahoot.Application.Features.Games.CreateGame;

using Kahoot.Application.Common.Messaging;

public sealed record CreateGameCommand(Guid QuizId) : ICommand<CreateGameResponse>;
