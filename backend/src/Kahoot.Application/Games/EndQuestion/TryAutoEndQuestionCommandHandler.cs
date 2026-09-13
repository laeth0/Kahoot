using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Games.Common;
using Kahoot.Domain.Common;
using Kahoot.Domain.Games;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Application.Games.EndQuestion;

internal sealed class TryAutoEndQuestionCommandHandler(
    IApplicationDbContext dbContext,
    TimeProvider timeProvider) : ICommandHandler<TryAutoEndQuestionCommand, QuestionResultsResponse?>
{
    public async Task<Result<QuestionResultsResponse?>> Handle(
        TryAutoEndQuestionCommand command,
        CancellationToken cancellationToken)
    {
        GameSession? game = await dbContext.GameSessions
            .FirstOrDefaultAsync(session => session.Id == command.GameId, cancellationToken);

        if (game is null || game.Status != GameStatus.QuestionActive)
        {
            return Result.Success<QuestionResultsResponse?>(null);
        }

        if (game.CurrentQuestionId is not { } questionId || game.CurrentQuestionIndex is not { } questionIndex)
        {
            return Result.Success<QuestionResultsResponse?>(null);
        }

        if (command.QuestionId.HasValue && command.QuestionId.Value != questionId)
        {
            return Result.Success<QuestionResultsResponse?>(null);
        }

        int totalAnswers = await dbContext.Answers
            .CountAsync(
                answer => answer.GameSessionId == game.Id && answer.QuestionId == questionId,
                cancellationToken);

        if (totalAnswers == 0)
        {
            return Result.Success<QuestionResultsResponse?>(null);
        }

        int remainingActiveUnanswered = await dbContext.Participants
            .CountAsync(
                participant => participant.GameSessionId == game.Id
                    && !participant.IsRemoved
                    && participant.ConnectionId != null
                    && !dbContext.Answers.Any(answer =>
                        answer.GameSessionId == game.Id
                        && answer.QuestionId == questionId
                        && answer.ParticipantId == participant.Id),
                cancellationToken);

        if (remainingActiveUnanswered > 0)
        {
            return Result.Success<QuestionResultsResponse?>(null);
        }

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
            return Result.Success<QuestionResultsResponse?>(null);
        }

        QuestionResultsResponse? results = await QuestionResultsBuilder.BuildAsync(
            dbContext, game.Id, questionId, questionIndex, cancellationToken);

        return Result.Success(results);
    }
}
