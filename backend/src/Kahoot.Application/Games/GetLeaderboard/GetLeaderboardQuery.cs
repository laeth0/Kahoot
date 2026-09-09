using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Games.Common;

namespace Kahoot.Application.Games.GetLeaderboard;

public sealed record GetLeaderboardQuery(Guid GameId) : IQuery<LeaderboardResponse>;
