using Kahoot.Application.Features.Quizzes.Questions;

namespace Kahoot.Application.Features.Quizzes.Questions.UpdateQuestion;

public sealed record UpdateQuestionRequest(
    string Text,
    Guid? ImageId,
    int DurationSeconds,
    int BasePoints,
    IReadOnlyList<ChoiceRequest> Choices);
