using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Games.Common;

namespace Kahoot.Application.Games.Reconnect;

public sealed record ReconnectParticipantCommand(string SessionToken) : ICommand<PlayerGameStateResponse>;
