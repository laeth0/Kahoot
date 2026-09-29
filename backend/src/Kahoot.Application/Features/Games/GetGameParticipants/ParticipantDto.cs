namespace Kahoot.Application.Features.Games.GetGameParticipants;

public sealed record ParticipantDto(
    Guid ParticipantId,
    string Nickname,
    int SeatNumber,
    bool IsRemoved,
    DateTimeOffset JoinedAt);
