using Kahoot.Application.Common.Messaging;

namespace Kahoot.Application.Features.Quizzes.GetQuizById;

public sealed record GetQuizByIdQuery(Guid QuizId) : IQuery<QuizDetailsResponse>;
