using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Games.Common;

namespace Kahoot.Application.Games.GetHostGameState;

public sealed record GetHostGameStateQuery(Guid GameId) : IQuery<HostGameStateResponse>;
