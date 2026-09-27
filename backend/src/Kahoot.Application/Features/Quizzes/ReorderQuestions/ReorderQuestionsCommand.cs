using Kahoot.Application.Common.Messaging;

namespace Kahoot.Application.Features.Quizzes.ReorderQuestions;

public sealed record ReorderQuestionsCommand(
    Guid QuizId,
    IReadOnlyList<Guid> QuestionIds) : ICommand<ReorderQuestionsResponse>;
