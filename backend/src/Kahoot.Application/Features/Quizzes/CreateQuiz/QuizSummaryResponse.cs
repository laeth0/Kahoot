namespace Kahoot.Application.Features.Quizzes.CreateQuiz;

public sealed record QuizSummaryResponse(
    Guid Id,
    string Title,
    string? Description,
    bool IsPublished,
    long Revision,
    int QuestionCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
