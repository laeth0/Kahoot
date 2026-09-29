namespace Kahoot.Application.Features.Games.GetGame;

public sealed record GetGameResponse(
    Guid GameId,
    string? Pin,
    string Title,
    string Status,
    long StateVersion,
    int? CurrentQuestionIndex,
    int TotalQuestions,
    int ParticipantCount,
    DateTimeOffset? HostGraceExpiresAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? FinishedAt);
