using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Observability;
using Kahoot.Application.Games.Common;
using Kahoot.Application.Games.Leaderboard;
using Kahoot.Domain.Common;
using Kahoot.Domain.Games;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Application.Games.EndGame;

internal sealed class AutoEndGameCommandHandler(
    IApplicationDbContext dbContext,
    ILeaderboardService leaderboardService,
    TimeProvider timeProvider,
    IKahootTelemetry telemetry) : ICommandHandler<AutoEndGameCommand, LeaderboardResponse?>
{
    public async Task<Result<LeaderboardResponse?>> Handle(
        AutoEndGameCommand command,
        CancellationToken cancellationToken)
    {
        GameSession? game = await dbContext.GameSessions
            .FirstOrDefaultAsync(session => session.Id == command.GameId, cancellationToken);

        if (game is null || game.Status == GameStatus.Finished)
        {
            return Result.Success<LeaderboardResponse?>(null);
        }

        List<Participant> participants = await dbContext.Participants
            .Where(participant => participant.GameSessionId == game.Id && !participant.IsRemoved)
            .ToListAsync(cancellationToken);

        game.Status = GameStatus.Finished;
        game.FinishedAt = timeProvider.GetUtcNow().UtcDateTime;
        LeaderboardResponse response = LeaderboardBuilder.ApplyRanks(participants, leaderboardService);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            telemetry.RecordGameEnded();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Success<LeaderboardResponse?>(null);
        }

        return Result.Success<LeaderboardResponse?>(response);
    }
}
