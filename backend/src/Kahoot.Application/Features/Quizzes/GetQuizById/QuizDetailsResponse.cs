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
    Guid? ImageId,
    string? ImageUrl,
    int DurationSeconds,
    int BasePoints,
    IReadOnlyList<QuizChoiceDetailsResponse> Choices);

public sealed record QuizDetailsResponse(
    Guid Id,
    string Title,
    string? Description,
    long Revision,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<QuizQuestionDetailsResponse> Questions);
