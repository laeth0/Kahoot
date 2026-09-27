using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Features.Quizzes;

namespace Kahoot.Application.Features.Quizzes.CreateQuiz;

public sealed record CreateQuizCommand(
    string Title,
    string? Description) : ICommand<QuizSummaryResponse>;
