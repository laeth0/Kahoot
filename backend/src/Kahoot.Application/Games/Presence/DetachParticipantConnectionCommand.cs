using Kahoot.Application.Common.Messaging;

namespace Kahoot.Application.Games.Presence;

public sealed record DetachParticipantConnectionCommand(Guid ParticipantId, string ConnectionId) : ICommand;
