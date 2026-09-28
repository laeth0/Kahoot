namespace Kahoot.Application.Features.Quizzes.Questions.AddQuestion;

public sealed record AddQuestionRequest(
    string Text,
    Guid? ImageId,
    int DurationSeconds,
    int BasePoints,
    IReadOnlyList<ChoiceRequest> Choices);
