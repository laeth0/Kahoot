using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Games.Common;

namespace Kahoot.Application.Games.StartNextQuestion;

public sealed record StartNextQuestionCommand(Guid GameId) : ICommand<QuestionStartedResponse>;
