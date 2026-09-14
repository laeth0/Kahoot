using System.Diagnostics;
using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Observability;
using Kahoot.Application.Common.Security;
using Kahoot.Application.Games.Common;
using Kahoot.Domain.Common;
using Kahoot.Domain.Games;
using Kahoot.Domain.Quizzes;
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
        Result<GameSession> gameResult = await HostGameGuard.LoadOwnedGameAsync(
            dbContext, currentUser, command.GameId, cancellationToken);
        if (gameResult.IsFailure)
        {
            return Result.Failure<QuestionStartedResponse>(gameResult.Error);
        }

        GameSession game = gameResult.Value;

        bool alreadyOnFirstQuestion = game.Status == GameStatus.QuestionActive && game.CurrentQuestionIndex == 0;
        if (!alreadyOnFirstQuestion && !GameStateMachine.CanFire(game.Status, GameTransition.StartFirstQuestion))
        {
            telemetry.RecordTransitionFailure("StartFirstQuestion", GameErrors.InvalidStateTransition.Code);
            return Result.Failure<QuestionStartedResponse>(GameErrors.InvalidStateTransition);
        }

        int totalQuestions = await dbContext.Questions
            .CountAsync(question => question.QuizId == game.QuizId, cancellationToken);
        if (totalQuestions == 0)
        {
            return Result.Failure<QuestionStartedResponse>(GameErrors.NoMoreQuestions);
        }

        int targetIndex = alreadyOnFirstQuestion ? game.CurrentQuestionIndex ?? 0 : 0;

        Question? question = await LoadQuestionAsync(game.QuizId, targetIndex, cancellationToken);
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

        GameStatus sourceState = game.Status;
        QuestionStartedResponse response = QuestionActivation.Activate(
            game, question, 0, totalQuestions, timeProvider.GetUtcNow());

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
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

    private Task<Question?> LoadQuestionAsync(Guid quizId, int orderIndex, CancellationToken cancellationToken) =>
        dbContext.Questions
            .AsNoTracking()
            .Include(question => question.Choices)
            .FirstOrDefaultAsync(
                question => question.QuizId == quizId && question.OrderIndex == orderIndex,
                cancellationToken);
}
