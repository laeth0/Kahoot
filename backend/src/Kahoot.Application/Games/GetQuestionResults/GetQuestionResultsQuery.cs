using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Games.Common;

namespace Kahoot.Application.Games.GetQuestionResults;

public sealed record GetQuestionResultsQuery(Guid GameId, Guid QuestionId) : IQuery<QuestionResultsResponse>;
