using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Quizzes.Common;

namespace Kahoot.Application.Quizzes.Questions.AddQuestion;

public sealed record AddQuestionCommand(
    Guid QuizId,
    string Text,
    string? ImageUrl,
    int TimeLimitSeconds,
    int Points,
    IReadOnlyList<ChoiceInput> Choices) : ICommand<Guid>;
