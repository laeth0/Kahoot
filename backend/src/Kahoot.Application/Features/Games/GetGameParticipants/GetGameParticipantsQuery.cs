namespace Kahoot.Application.Features.Games.GetGameParticipants;

using Kahoot.Application.Common.Messaging;

public sealed record GetGameParticipantsQuery(
    Guid GameId,
    bool IncludeRemoved = false,
    int? Limit = null,
    int? Cursor = null) : IQuery<GetGameParticipantsResponse>;
