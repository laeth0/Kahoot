using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Quizzes.Common;

namespace Kahoot.Application.Quizzes.GetQuiz;

public sealed record GetQuizQuery(Guid QuizId) : IQuery<QuizDetailResponse>;
