using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Features.Quizzes.Questions;

namespace Kahoot.Application.Features.Quizzes.Questions.UpdateQuestion;

public sealed record UpdateQuestionCommand(
    Guid QuizId,
    Guid QuestionId,
    string Text,
    Guid? ImageId,
    int DurationSeconds,
    int BasePoints,
    IReadOnlyList<ChoiceRequest> Choices) : ICommand<QuestionResponse>;
