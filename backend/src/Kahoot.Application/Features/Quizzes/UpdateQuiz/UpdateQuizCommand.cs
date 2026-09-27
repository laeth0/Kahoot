using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Features.Quizzes;

namespace Kahoot.Application.Features.Quizzes.UpdateQuiz;

public sealed record UpdateQuizCommand(
    Guid QuizId,
    string Title,
    string? Description) : ICommand<QuizSummaryResponse>;
