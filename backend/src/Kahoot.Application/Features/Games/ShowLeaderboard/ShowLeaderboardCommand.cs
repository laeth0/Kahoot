namespace Kahoot.Application.Features.Games.ShowLeaderboard;

using Kahoot.Application.Common.Messaging;

public sealed record ShowLeaderboardCommand(
    Guid GameId,
    Guid CommandId,
    long ExpectedStateVersion) : ICommand<ShowLeaderboardResponse>;
