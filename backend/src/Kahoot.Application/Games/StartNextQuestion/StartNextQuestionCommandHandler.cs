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

        if (game.Status is not (GameStatus.Leaderboard or GameStatus.QuestionActive))
        {
            return Result.Failure<QuestionStartedResponse>(GameErrors.InvalidStateTransition);
        }

        List<Question> questions = await dbContext.Questions
            .Where(question => question.QuizId == game.QuizId)
            .OrderBy(question => question.OrderIndex)
            .Include(question => question.Choices)
            .ToListAsync(cancellationToken);

        if (game.Status == GameStatus.QuestionActive)
        {
            return Result.Success(QuestionActivation.Rebuild(game, questions));
        }

        int nextIndex = (game.CurrentQuestionIndex ?? -1) + 1;
        if (nextIndex >= questions.Count)
        {
            return Result.Failure<QuestionStartedResponse>(GameErrors.NoMoreQuestions);
        }

        QuestionStartedResponse response =
            QuestionActivation.Activate(game, questions, nextIndex, timeProvider.GetUtcNow());

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
}
