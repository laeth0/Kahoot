namespace Kahoot.Application.Features.Games.GetGameReport;

using Kahoot.Application.Features.Games.Models;

public sealed record GetGameReportResponse(
    Guid GameId,
    string Title,
    string Status,
    int TotalQuestions,
    int TotalParticipants,
    DateTimeOffset CreatedAt,
    DateTimeOffset? FinishedAt,
    List<QuestionReportDto> Questions,
    List<LeaderboardParticipantDto> Leaderboard);
