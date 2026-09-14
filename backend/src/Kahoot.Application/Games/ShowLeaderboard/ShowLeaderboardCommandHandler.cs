using System.Diagnostics;
using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Observability;
using Kahoot.Application.Common.Security;
using Kahoot.Application.Games.Common;
using Kahoot.Application.Games.Leaderboard;
using Kahoot.Domain.Common;
using Kahoot.Domain.Games;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Application.Games.ShowLeaderboard;

internal sealed class ShowLeaderboardCommandHandler(
    IApplicationDbContext dbContext,
    ICurrentUser currentUser,
    ILeaderboardService leaderboardService,
    IKahootTelemetry telemetry) : ICommandHandler<ShowLeaderboardCommand, LeaderboardResponse>
{
    public async Task<Result<LeaderboardResponse>> Handle(
        ShowLeaderboardCommand command,
        CancellationToken cancellationToken)
    {
        Result<GameSession> gameResult = await HostGameGuard.LoadOwnedGameAsync(
            dbContext, currentUser, command.GameId, cancellationToken);
        if (gameResult.IsFailure)
        {
            return Result.Failure<LeaderboardResponse>(gameResult.Error);
        }

        GameSession game = gameResult.Value;

        if (game.Status == GameStatus.Leaderboard)
        {
            Activity.Current?.SetTag("game.id", game.Id);
            Activity.Current?.SetTag("transition", "ShowLeaderboard");
            Activity.Current?.SetTag("game.source_state", game.Status.ToString());
            Activity.Current?.SetTag("game.resulting_state", game.Status.ToString());
            return Result.Success(await LeaderboardBuilder.ReadAsync(dbContext, game.Id, cancellationToken));
        }

        if (!GameStateMachine.CanFire(game.Status, GameTransition.ShowLeaderboard))
        {
            telemetry.RecordTransitionFailure("ShowLeaderboard", GameErrors.InvalidStateTransition.Code);
            return Result.Failure<LeaderboardResponse>(GameErrors.InvalidStateTransition);
        }

        List<Participant> participants = await dbContext.Participants
            .Where(participant => participant.GameSessionId == game.Id && !participant.IsRemoved)
            .ToListAsync(cancellationToken);

        GameStatus sourceState = game.Status;
        game.Status = GameStatus.Leaderboard;
        LeaderboardResponse response = LeaderboardBuilder.ApplyRanks(participants, leaderboardService);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            Activity.Current?.SetTag("game.id", game.Id);
            Activity.Current?.SetTag("transition", "ShowLeaderboard");
            Activity.Current?.SetTag("game.source_state", sourceState.ToString());
            Activity.Current?.SetTag("game.resulting_state", game.Status.ToString());
        }
        catch (DbUpdateConcurrencyException)
        {
            telemetry.RecordTransitionFailure("ShowLeaderboard", GameErrors.ConcurrentModification.Code);
            return Result.Failure<LeaderboardResponse>(GameErrors.ConcurrentModification);
        }

        return Result.Success(response);
    }
}
