namespace Kahoot.Application.Features.Quizzes;

public sealed record QuizSummaryResponse(
    Guid Id,
    string Title,
    string? Description,
    long Revision,
    int QuestionCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
