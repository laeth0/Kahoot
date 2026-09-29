namespace Kahoot.Application.Features.Games.ShowLeaderboard;

using Kahoot.Application.Features.Games.Models;

public sealed record ShowLeaderboardResponse(
    Guid GameId,
    string Status,
    long StateVersion,
    List<LeaderboardParticipantDto> TopParticipants);
