namespace Kahoot.Application.Features.Quizzes.Questions;

public sealed record ChoiceResponse(
    Guid Id,
    int OrderIndex,
    string Text,
    bool IsCorrect);

public sealed record QuestionResponse(
    Guid Id,
    Guid QuizId,
    int OrderIndex,
    string Text,
    Guid? ImageId,
    int DurationSeconds,
    int BasePoints,
    IReadOnlyList<ChoiceResponse> Choices);
