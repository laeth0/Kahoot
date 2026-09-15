using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Games.Common;

namespace Kahoot.Application.Games.EndGame;

public sealed record AutoEndGameCommand(Guid GameId) : ICommand<LeaderboardResponse?>;
