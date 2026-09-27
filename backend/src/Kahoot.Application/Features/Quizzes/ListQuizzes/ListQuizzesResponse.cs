namespace Kahoot.Application.Features.Quizzes.ListQuizzes;

public sealed record ListQuizzesResponse(
    IReadOnlyList<QuizSummaryResponse> Items,
    string? NextCursor,
    bool HasMore);
