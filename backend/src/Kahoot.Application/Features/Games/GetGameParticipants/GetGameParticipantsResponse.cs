namespace Kahoot.Application.Features.Games.GetGameParticipants;

public sealed record GetGameParticipantsResponse(
    Guid GameId,
    long PresenceVersion,
    int ReservedParticipantCount,
    List<ParticipantDto> Participants,
    int? NextCursor);
