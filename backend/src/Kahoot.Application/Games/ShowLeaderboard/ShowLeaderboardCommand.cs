using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Games.Common;

namespace Kahoot.Application.Games.ShowLeaderboard;

public sealed record ShowLeaderboardCommand(Guid GameId) : ICommand<LeaderboardResponse>;
