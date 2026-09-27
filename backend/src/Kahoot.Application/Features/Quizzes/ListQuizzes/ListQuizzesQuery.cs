using Kahoot.Application.Common.Messaging;

namespace Kahoot.Application.Features.Quizzes.ListQuizzes;

public sealed record ListQuizzesQuery(
    string? Cursor = null,
    int PageSize = 50) : IQuery<ListQuizzesResponse>;
