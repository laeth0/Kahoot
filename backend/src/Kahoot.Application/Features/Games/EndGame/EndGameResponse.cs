namespace Kahoot.Application.Features.Games.EndGame;

using Kahoot.Application.Features.Games.Models;

public sealed record EndGameResponse(
    Guid GameId,
    string Status,
    long StateVersion,
    DateTimeOffset FinishedAt,
    List<PodiumParticipantDto> Podium);
