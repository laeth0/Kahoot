using Kahoot.Application.Common.Messaging;

namespace Kahoot.Application.Features.Quizzes.CreateQuiz;

public sealed record CreateQuizCommand(
    string Title,
    string? Description) : ICommand<QuizSummaryResponse>;
