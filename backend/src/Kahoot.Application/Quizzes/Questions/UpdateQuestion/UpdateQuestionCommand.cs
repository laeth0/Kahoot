using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Quizzes.Common;

namespace Kahoot.Application.Quizzes.Questions.UpdateQuestion;

public sealed record UpdateQuestionCommand(
    Guid QuizId,
    Guid QuestionId,
    string Text,
    string? ImageUrl,
    int TimeLimitSeconds,
    int Points,
    IReadOnlyList<ChoiceInput> Choices) : ICommand;
