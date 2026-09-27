using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Features.Quizzes.Questions;

namespace Kahoot.Application.Features.Quizzes.Questions.AddQuestion;

public sealed record AddQuestionCommand(
    Guid QuizId,
    string Text,
    Guid? MediaId,
    int DurationSeconds,
    int BasePoints,
    IReadOnlyList<ChoiceRequest> Choices) : ICommand<QuestionResponse>;
