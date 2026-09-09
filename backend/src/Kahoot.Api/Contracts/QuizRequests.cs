using Kahoot.Application.Quizzes.Common;

namespace Kahoot.Api.Contracts;

public sealed record CreateQuizRequest(string Title, string? Description);

public sealed record UpdateQuizRequest(string Title, string? Description);

public sealed record SaveQuestionRequest(
    string Text,
    string? ImageUrl,
    int TimeLimitSeconds,
    int Points,
    IReadOnlyList<ChoiceInput> Choices);

public sealed record ReorderQuestionsRequest(IReadOnlyList<Guid> OrderedQuestionIds);
