using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Games.Common;

namespace Kahoot.Application.Games.Presence;

public sealed record DetachParticipantConnectionCommand(
    Guid GameId,
    Guid ParticipantId,
    string ConnectionId) : ICommand<ParticipantPresenceMutationResponse?>;
