using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Games.Common;

namespace Kahoot.Application.Games.EndQuestion;

public sealed record TryAutoEndQuestionCommand(Guid GameId, Guid? QuestionId = null)
    : ICommand<QuestionResultsResponse?>;
