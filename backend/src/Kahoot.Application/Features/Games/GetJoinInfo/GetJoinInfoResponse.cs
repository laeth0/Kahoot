namespace Kahoot.Application.Features.Games.GetJoinInfo;

public sealed record GetJoinInfoResponse(
    Guid GameId,
    string Title,
    string Status,
    int ParticipantCount,
    int MaxCapacity,
    bool IsFull);
