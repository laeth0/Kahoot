namespace Kahoot.Application.Features.Quizzes.GetQuizById;

public sealed record QuizChoiceDetailsResponse(
    Guid Id,
    int OrderIndex,
    string Text,
    bool IsCorrect);

public sealed record QuizQuestionDetailsResponse(
    Guid Id,
    int OrderIndex,
    string Text,
    Guid? MediaId,
    int DurationSeconds,
    int BasePoints,
    IReadOnlyList<QuizChoiceDetailsResponse> Choices);

public sealed record QuizDetailsResponse(
    Guid Id,
    string Title,
    string? Description,
    bool IsPublished,
    long Revision,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<QuizQuestionDetailsResponse> Questions);
