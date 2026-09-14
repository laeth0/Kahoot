using Kahoot.Application.Common.Messaging;

namespace Kahoot.Application.Games.RemoveParticipant;

public sealed record RemoveParticipantResponse(string? ConnectionId);

public sealed record RemoveParticipantCommand(Guid GameId, Guid ParticipantId) : ICommand<RemoveParticipantResponse>;
