using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Messaging;
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
    ILeaderboardService leaderboardService) : ICommandHandler<ShowLeaderboardCommand, LeaderboardResponse>
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
            return Result.Success(await LeaderboardBuilder.ReadAsync(dbContext, game.Id, cancellationToken));
        }

        if (game.Status != GameStatus.QuestionResults)
        {
            return Result.Failure<LeaderboardResponse>(GameErrors.InvalidStateTransition);
        }

        List<Participant> participants = await dbContext.Participants
            .Where(participant => participant.GameSessionId == game.Id && !participant.IsRemoved)
            .ToListAsync(cancellationToken);

        game.Status = GameStatus.Leaderboard;
        LeaderboardResponse response = LeaderboardBuilder.ApplyRanks(participants, leaderboardService);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<LeaderboardResponse>(GameErrors.ConcurrentModification);
        }

        return Result.Success(response);
    }
}
