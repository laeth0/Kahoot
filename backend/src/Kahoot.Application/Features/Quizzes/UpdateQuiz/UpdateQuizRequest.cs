namespace Kahoot.Application.Features.Quizzes.UpdateQuiz;

public sealed record UpdateQuizRequest(
    string Title,
    string? Description);
