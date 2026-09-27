namespace Kahoot.Application.Features.Quizzes.Questions;

public sealed record ChoiceRequest(
    string Text,
    bool IsCorrect);
