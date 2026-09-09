using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Security;
using Kahoot.Application.Games.Common;
using Kahoot.Domain.Common;
using Kahoot.Domain.Games;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Application.Games.EndQuestion;

internal sealed class EndQuestionCommandHandler(
    IApplicationDbContext dbContext,
    ICurrentUser currentUser,
    TimeProvider timeProvider) : ICommandHandler<EndQuestionCommand, QuestionResultsResponse>
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

        if (game.Status is not (GameStatus.QuestionActive or GameStatus.QuestionResults))
        {
            return Result.Failure<QuestionResultsResponse>(GameErrors.InvalidStateTransition);
        }

        if (game.CurrentQuestionId is not { } questionId || game.CurrentQuestionIndex is not { } questionIndex)
        {
            return Result.Failure<QuestionResultsResponse>(GameErrors.InvalidStateTransition);
        }

        if (game.Status == GameStatus.QuestionActive)
        {
            DateTimeOffset now = timeProvider.GetUtcNow();
            if (game.CurrentQuestionEndsAt is { } endsAt && now.UtcDateTime < endsAt)
            {
                game.CurrentQuestionEndsAt = now.UtcDateTime;
            }

            game.Status = GameStatus.QuestionResults;

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
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
