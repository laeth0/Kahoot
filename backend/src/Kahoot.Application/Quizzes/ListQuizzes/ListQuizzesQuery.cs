using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Quizzes.Common;

namespace Kahoot.Application.Quizzes.ListQuizzes;

public sealed record ListQuizzesQuery : IQuery<IReadOnlyList<QuizSummaryResponse>>;
