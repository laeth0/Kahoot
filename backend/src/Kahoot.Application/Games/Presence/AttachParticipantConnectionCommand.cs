using Kahoot.Application.Common.Messaging;

namespace Kahoot.Application.Games.Presence;

public sealed record AttachParticipantConnectionCommand(Guid ParticipantId, string ConnectionId) : ICommand;
