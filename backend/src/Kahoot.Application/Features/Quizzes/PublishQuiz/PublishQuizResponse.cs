namespace Kahoot.Application.Features.Quizzes.PublishQuiz;

public sealed record PublishQuizResponse(
    Guid Id,
    bool IsPublished,
    long Revision,
    DateTimeOffset PublishedAt);
