using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Games.Common;

namespace Kahoot.Application.Games.RemoveParticipant;

public sealed record RemoveParticipantResponse(
    string? ConnectionId,
    ParticipantPresenceResponse? Presence);

public sealed record RemoveParticipantCommand(Guid GameId, Guid ParticipantId) : ICommand<RemoveParticipantResponse>;
