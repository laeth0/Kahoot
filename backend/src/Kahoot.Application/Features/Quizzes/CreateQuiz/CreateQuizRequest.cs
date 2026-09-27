namespace Kahoot.Application.Features.Quizzes.CreateQuiz;

public sealed record CreateQuizRequest(
    string Title,
    string? Description);
