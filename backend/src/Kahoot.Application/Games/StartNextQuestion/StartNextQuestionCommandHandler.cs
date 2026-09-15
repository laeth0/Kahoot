using System.Diagnostics;
using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Observability;
using Kahoot.Application.Common.Security;
using Kahoot.Application.Games.Common;
using Kahoot.Domain.Common;
using Kahoot.Domain.Games;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kahoot.Application.Games.StartNextQuestion;

internal sealed class StartNextQuestionCommandHandler(
    IApplicationDbContext dbContext,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    IKahootTelemetry telemetry,
    ILogger<StartNextQuestionCommandHandler> logger) : ICommandHandler<StartNextQuestionCommand, QuestionStartedResponse>
{
    public async Task<Result<QuestionStartedResponse>> Handle(
        StartNextQuestionCommand command,
        CancellationToken cancellationToken)
    {
        Result<GameSession> gameResult = await HostGameGuard.LoadOwnedGameAsync(
            dbContext, currentUser, command.GameId, cancellationToken);
        if (gameResult.IsFailure)
        {
            return Result.Failure<QuestionStartedResponse>(gameResult.Error);
        }

        GameSession game = gameResult.Value;

        bool reentry = game.Status == GameStatus.QuestionActive;
        if (!reentry && !GameStateMachine.CanFire(game.Status, GameTransition.AdvanceToNextQuestion))
        {
            return FailAdvance(
                game,
                GameErrors.InvalidStateTransition,
                game.CurrentQuestionIndex,
                null,
                "invalid source state");
        }

        int totalQuestions = await dbContext.GameQuestionSnapshots
            .CountAsync(question => question.GameSessionId == game.Id, cancellationToken);

        if (reentry)
        {
            if (game.CurrentQuestionId is not { } currentQuestionId ||
                game.CurrentQuestionIndex is not { } activeQuestionIndex ||
                activeQuestionIndex < 0 ||
                activeQuestionIndex >= totalQuestions)
            {
                return FailAdvance(
                    game,
                    GameErrors.InvalidStateTransition,
                    game.CurrentQuestionIndex,
                    totalQuestions,
                    "active game has no current question");
            }

            GameQuestionSnapshot? current = await LoadQuestionAsync(game.Id, currentQuestionId, cancellationToken);

            if (current is null)
            {
                return FailAdvance(
                    game,
                    GameErrors.InvalidStateTransition,
                    game.CurrentQuestionIndex,
                    totalQuestions,
                    "current question snapshot is missing");
            }

            Activity.Current?.SetTag("game.id", game.Id);
            Activity.Current?.SetTag("transition", "AdvanceToNextQuestion");
            Activity.Current?.SetTag("game.source_state", game.Status.ToString());
            Activity.Current?.SetTag("game.resulting_state", game.Status.ToString());
            return Result.Success(QuestionActivation.Rebuild(game, current, totalQuestions));
        }

        if (game.CurrentQuestionIndex is not { } currentQuestionIndex ||
            currentQuestionIndex < 0 ||
            currentQuestionIndex >= totalQuestions)
        {
            return FailAdvance(
                game,
                GameErrors.InvalidStateTransition,
                null,
                totalQuestions,
                "completed question state has no current question index");
        }

        int nextIndex = currentQuestionIndex + 1;
        if (nextIndex >= totalQuestions)
        {
            return FailAdvance(
                game,
                GameErrors.NoMoreQuestions,
                nextIndex,
                totalQuestions,
                "requested position is past the final question");
        }

        GameQuestionSnapshot? question = await LoadQuestionAtPositionAsync(game.Id, nextIndex, cancellationToken);
        if (question is null)
        {
            return FailAdvance(
                game,
                GameErrors.InvalidStateTransition,
                nextIndex,
                totalQuestions,
                "question snapshot is missing before the final position");
        }

        int eligibleCount = await dbContext.Participants
            .CountAsync(participant => participant.GameSessionId == game.Id && !participant.IsRemoved, cancellationToken);

        GameStatus sourceState = game.Status;
        QuestionStartedResponse response = QuestionActivation.Activate(
            game, question, nextIndex, totalQuestions, eligibleCount, timeProvider.GetUtcNow());

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            Activity.Current?.SetTag("game.id", game.Id);
            Activity.Current?.SetTag("transition", "AdvanceToNextQuestion");
            Activity.Current?.SetTag("game.source_state", sourceState.ToString());
            Activity.Current?.SetTag("game.resulting_state", game.Status.ToString());
            telemetry.RecordQuestionServed("advance");
        }
        catch (DbUpdateConcurrencyException)
        {
            return FailAdvance(
                game,
                GameErrors.ConcurrentModification,
                nextIndex,
                totalQuestions,
                "game state changed during advance");
        }

        return Result.Success(response);
    }

    private Result<QuestionStartedResponse> FailAdvance(
        GameSession game,
        Error error,
        int? requestedIndex,
        int? totalQuestions,
        string reason)
    {
        Activity.Current?.SetTag("game.id", game.Id);
        Activity.Current?.SetTag("transition", "AdvanceToNextQuestion");
        Activity.Current?.SetTag("game.source_state", game.Status.ToString());
        Activity.Current?.SetTag("game.current_question_index", game.CurrentQuestionIndex);
        Activity.Current?.SetTag("game.requested_question_index", requestedIndex);
        Activity.Current?.SetTag("game.total_questions", totalQuestions);
        Activity.Current?.SetTag("error.code", error.Code);
        telemetry.RecordTransitionFailure("AdvanceToNextQuestion", error.Code);
        logger.LogWarning(
            "Cannot advance game {GameId} from {GameStatus}: {Reason}. Current index {CurrentQuestionIndex}, requested index {RequestedQuestionIndex}, total questions {TotalQuestions}, error {ErrorCode}",
            game.Id,
            game.Status,
            reason,
            game.CurrentQuestionIndex,
            requestedIndex,
            totalQuestions,
            error.Code);
        return Result.Failure<QuestionStartedResponse>(error);
    }

    private Task<GameQuestionSnapshot?> LoadQuestionAsync(
        Guid gameSessionId,
        Guid questionId,
        CancellationToken cancellationToken) =>
        dbContext.GameQuestionSnapshots
            .AsNoTracking()
            .Include(question => question.Choices)
            .FirstOrDefaultAsync(
                question => question.GameSessionId == gameSessionId && question.Id == questionId,
                cancellationToken);

    private Task<GameQuestionSnapshot?> LoadQuestionAtPositionAsync(
        Guid gameSessionId,
        int position,
        CancellationToken cancellationToken) =>
        dbContext.GameQuestionSnapshots
            .AsNoTracking()
            .Where(question => question.GameSessionId == gameSessionId)
            .OrderBy(question => question.OrderIndex)
            .Skip(position)
            .Include(question => question.Choices)
            .FirstOrDefaultAsync(cancellationToken);
}
