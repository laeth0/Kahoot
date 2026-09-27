namespace Kahoot.Application.Features.Quizzes.ReorderQuestions;

public sealed record ReorderQuestionsRequest(
    IReadOnlyList<Guid> QuestionIds);
