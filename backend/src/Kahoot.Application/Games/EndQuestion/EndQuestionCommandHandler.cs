using System.Diagnostics;
using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Observability;
using Kahoot.Application.Common.Security;
using Kahoot.Application.Games.Common;
using Kahoot.Domain.Common;
using Kahoot.Domain.Games;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Application.Games.EndQuestion;

internal sealed class EndQuestionCommandHandler(
    IApplicationDbContext dbContext,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IKahootTelemetry telemetry) : ICommandHandler<EndQuestionCommand, QuestionResultsResponse>
{
    public async Task<Result<QuestionResultsResponse>> Handle(
        EndQuestionCommand command,
        CancellationToken cancellationToken)
    {
        Result<GameSession> gameResult = await HostGameGuard.LoadOwnedGameAsync(
            dbContext, currentUser, command.GameId, cancellationToken);
        if (gameResult.IsFailure)
        {
            return Result.Failure<QuestionResultsResponse>(gameResult.Error);
        }

        GameSession game = gameResult.Value;

        bool reentry = game.Status == GameStatus.QuestionResults;
        if (!reentry && !GameStateMachine.CanFire(game.Status, GameTransition.RevealQuestionResults))
        {
            telemetry.RecordTransitionFailure("RevealQuestionResults", GameErrors.InvalidStateTransition.Code);
            return Result.Failure<QuestionResultsResponse>(GameErrors.InvalidStateTransition);
        }

        if (game.CurrentQuestionId is not { } questionId || game.CurrentQuestionIndex is not { } questionIndex)
        {
            telemetry.RecordTransitionFailure("RevealQuestionResults", GameErrors.InvalidStateTransition.Code);
            return Result.Failure<QuestionResultsResponse>(GameErrors.InvalidStateTransition);
        }

        if (reentry)
        {
            Activity.Current?.SetTag("game.id", game.Id);
            Activity.Current?.SetTag("transition", "RevealQuestionResults");
            Activity.Current?.SetTag("game.source_state", game.Status.ToString());
            Activity.Current?.SetTag("game.resulting_state", game.Status.ToString());
        }
        else if (game.Status == GameStatus.QuestionActive)
        {
            GameStatus sourceState = game.Status;
            DateTimeOffset now = timeProvider.GetUtcNow();
            if (game.CurrentQuestionEndsAt is { } endsAt && now.UtcDateTime < endsAt)
            {
                game.CurrentQuestionEndsAt = now.UtcDateTime;
            }

            game.Status = GameStatus.QuestionResults;

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                Activity.Current?.SetTag("game.id", game.Id);
                Activity.Current?.SetTag("transition", "RevealQuestionResults");
                Activity.Current?.SetTag("game.source_state", sourceState.ToString());
                Activity.Current?.SetTag("game.resulting_state", game.Status.ToString());
            }
            catch (DbUpdateConcurrencyException)
            {
                telemetry.RecordTransitionFailure("RevealQuestionResults", GameErrors.ConcurrentModification.Code);
                return Result.Failure<QuestionResultsResponse>(GameErrors.ConcurrentModification);
            }
        }

        QuestionResultsResponse? results = await QuestionResultsBuilder.BuildAsync(
            dbContext, game.Id, questionId, questionIndex, cancellationToken);

        return results is null
            ? Result.Failure<QuestionResultsResponse>(GameErrors.InvalidStateTransition)
            : Result.Success(results);
    }
}
