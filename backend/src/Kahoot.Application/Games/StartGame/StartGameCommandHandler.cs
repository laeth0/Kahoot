using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Messaging;
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
    TimeProvider timeProvider) : ICommandHandler<StartGameCommand, QuestionStartedResponse>
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
        if (game.Status != GameStatus.Lobby && !alreadyOnFirstQuestion)
        {
            return Result.Failure<QuestionStartedResponse>(GameErrors.InvalidStateTransition);
        }

        List<Question> questions = await dbContext.Questions
            .Where(question => question.QuizId == game.QuizId)
            .OrderBy(question => question.OrderIndex)
            .Include(question => question.Choices)
            .ToListAsync(cancellationToken);

        if (questions.Count == 0)
        {
            return Result.Failure<QuestionStartedResponse>(GameErrors.NoMoreQuestions);
        }

        if (alreadyOnFirstQuestion)
        {
            return Result.Success(QuestionActivation.Rebuild(game, questions));
        }

        QuestionStartedResponse response = QuestionActivation.Activate(game, questions, 0, timeProvider.GetUtcNow());

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
