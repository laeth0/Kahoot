using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Games.Common;

namespace Kahoot.Application.Games.Presence;

public sealed record AttachParticipantConnectionCommand(
    Guid GameId,
    Guid ParticipantId,
    string ConnectionId,
    string Reason) : ICommand<ParticipantPresenceMutationResponse>;
