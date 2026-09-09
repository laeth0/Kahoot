using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Security;
using Kahoot.Application.Games.Common;
using Kahoot.Domain.Common;
using Kahoot.Domain.Games;
using Kahoot.Domain.Quizzes;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Application.Games.StartNextQuestion;

internal sealed class StartNextQuestionCommandHandler(
    IApplicationDbContext dbContext,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : ICommandHandler<StartNextQuestionCommand, QuestionStartedResponse>
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
            return Result.Failure<QuestionStartedResponse>(GameErrors.InvalidStateTransition);
        }

        int totalQuestions = await dbContext.Questions
            .CountAsync(question => question.QuizId == game.QuizId, cancellationToken);

        if (reentry)
        {
            Question? current = await LoadQuestionAsync(
                game.QuizId, game.CurrentQuestionIndex ?? 0, cancellationToken);

            return current is null
                ? Result.Failure<QuestionStartedResponse>(GameErrors.NoMoreQuestions)
                : Result.Success(QuestionActivation.Rebuild(game, current, totalQuestions));
        }

        int nextIndex = (game.CurrentQuestionIndex ?? -1) + 1;
        if (nextIndex >= totalQuestions)
        {
            return Result.Failure<QuestionStartedResponse>(GameErrors.NoMoreQuestions);
        }

        Question? question = await LoadQuestionAsync(game.QuizId, nextIndex, cancellationToken);
        if (question is null)
        {
            return Result.Failure<QuestionStartedResponse>(GameErrors.NoMoreQuestions);
        }

        QuestionStartedResponse response = QuestionActivation.Activate(
            game, question, nextIndex, totalQuestions, timeProvider.GetUtcNow());

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
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
