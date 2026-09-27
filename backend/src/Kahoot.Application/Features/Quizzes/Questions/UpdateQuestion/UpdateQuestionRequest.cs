using Kahoot.Application.Features.Quizzes.Questions;

namespace Kahoot.Application.Features.Quizzes.Questions.UpdateQuestion;

public sealed record UpdateQuestionRequest(
    string Text,
    Guid? MediaId,
    int DurationSeconds,
    int BasePoints,
    IReadOnlyList<ChoiceRequest> Choices);
