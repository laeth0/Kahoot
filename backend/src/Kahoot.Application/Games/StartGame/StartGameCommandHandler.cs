using System.Diagnostics;
using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Errors;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Observability;
using Kahoot.Application.Common.Security;
using Kahoot.Application.Games.Common;
using Kahoot.Domain.Common;
using Kahoot.Domain.Games;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Application.Games.StartGame;

internal sealed class StartGameCommandHandler(
    IApplicationDbContext dbContext,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IKahootTelemetry telemetry) : ICommandHandler<StartGameCommand, QuestionStartedResponse>
{
    public async Task<Result<QuestionStartedResponse>> Handle(StartGameCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.HostId is not { } hostId)
        {
            return Result.Failure<QuestionStartedResponse>(SharedErrors.Unauthorized);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        GameSession? game = await dbContext.GameSessions
            .FirstOrDefaultAsync(session => session.Id == command.GameId && session.HostId == hostId, cancellationToken);

        if (game is null)
        {
            return Result.Failure<QuestionStartedResponse>(GameErrors.NotFound);
        }

        bool alreadyOnFirstQuestion = game.Status == GameStatus.QuestionActive && game.CurrentQuestionIndex == 0;
        if (!alreadyOnFirstQuestion && !GameStateMachine.CanFire(game.Status, GameTransition.StartFirstQuestion))
        {
            telemetry.RecordTransitionFailure("StartFirstQuestion", GameErrors.InvalidStateTransition.Code);
            return Result.Failure<QuestionStartedResponse>(GameErrors.InvalidStateTransition);
        }

        int totalQuestions = await dbContext.GameQuestionSnapshots
            .CountAsync(question => question.GameSessionId == game.Id, cancellationToken);
        if (totalQuestions == 0)
        {
            return Result.Failure<QuestionStartedResponse>(GameErrors.NoMoreQuestions);
        }

        int targetIndex = alreadyOnFirstQuestion ? game.CurrentQuestionIndex ?? 0 : 0;

        GameQuestionSnapshot? question = await LoadQuestionAsync(game.Id, targetIndex, cancellationToken);
        if (question is null)
        {
            return Result.Failure<QuestionStartedResponse>(GameErrors.NoMoreQuestions);
        }

        if (alreadyOnFirstQuestion)
        {
            Activity.Current?.SetTag("game.id", game.Id);
            Activity.Current?.SetTag("transition", "StartFirstQuestion");
            Activity.Current?.SetTag("game.source_state", game.Status.ToString());
            Activity.Current?.SetTag("game.resulting_state", game.Status.ToString());
            return Result.Success(QuestionActivation.Rebuild(game, question, totalQuestions));
        }

        int eligibleCount = await dbContext.Participants
            .CountAsync(participant => participant.GameSessionId == game.Id && !participant.IsRemoved, cancellationToken);

        GameStatus sourceState = game.Status;
        QuestionStartedResponse response = QuestionActivation.Activate(
            game, question, 0, totalQuestions, eligibleCount, timeProvider.GetUtcNow());

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            Activity.Current?.SetTag("game.id", game.Id);
            Activity.Current?.SetTag("transition", "StartFirstQuestion");
            Activity.Current?.SetTag("game.source_state", sourceState.ToString());
            Activity.Current?.SetTag("game.resulting_state", game.Status.ToString());
            telemetry.RecordQuestionServed("start");
        }
        catch (DbUpdateConcurrencyException)
        {
            telemetry.RecordTransitionFailure("StartFirstQuestion", GameErrors.ConcurrentModification.Code);
            return Result.Failure<QuestionStartedResponse>(GameErrors.ConcurrentModification);
        }

        return Result.Success(response);
    }

    private Task<GameQuestionSnapshot?> LoadQuestionAsync(Guid gameSessionId, int orderIndex, CancellationToken cancellationToken) =>
        dbContext.GameQuestionSnapshots
            .AsNoTracking()
            .Include(question => question.Choices)
            .FirstOrDefaultAsync(
                question => question.GameSessionId == gameSessionId && question.OrderIndex == orderIndex,
                cancellationToken);
}
