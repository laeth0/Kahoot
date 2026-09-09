namespace Kahoot.Application.Quizzes.Common;

public sealed record QuizSummaryResponse(
    Guid Id,
    string Title,
    string? Description,
    bool IsPublished,
    int QuestionCount);

public sealed record QuizDetailResponse(
    Guid Id,
    string Title,
    string? Description,
    bool IsPublished,
    IReadOnlyList<QuestionResponse> Questions);

public sealed record QuestionResponse(
    Guid Id,
    int OrderIndex,
    string Text,
    string? ImageUrl,
    int TimeLimitSeconds,
    int Points,
    IReadOnlyList<ChoiceResponse> Choices);

public sealed record ChoiceResponse(
    Guid Id,
    int OrderIndex,
    string? Text,
    string? ImageUrl,
    bool IsCorrect);

public sealed record ChoiceInput(string? Text, string? ImageUrl, bool IsCorrect);
