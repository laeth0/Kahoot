using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Errors;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Security;
using Kahoot.Application.Games.Common;
using Kahoot.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Application.Games.GetQuestionResults;

internal sealed class GetQuestionResultsQueryHandler(IApplicationDbContext dbContext, ICurrentUser currentUser)
    : IQueryHandler<GetQuestionResultsQuery, QuestionResultsResponse>
{
    public async Task<Result<QuestionResultsResponse>> Handle(
        GetQuestionResultsQuery query,
        CancellationToken cancellationToken)
    {
        if (currentUser.HostId is not { } hostId)
        {
            return Result.Failure<QuestionResultsResponse>(SharedErrors.Unauthorized);
        }

        var game = await dbContext.GameSessions
            .AsNoTracking()
            .Where(session => session.Id == query.GameId && session.HostId == hostId)
            .Select(session => new { session.QuizId })
            .FirstOrDefaultAsync(cancellationToken);

        if (game is null)
        {
            return Result.Failure<QuestionResultsResponse>(GameErrors.NotFound);
        }

        int? questionIndex = await dbContext.Questions
            .AsNoTracking()
            .Where(question => question.Id == query.QuestionId && question.QuizId == game.QuizId)
            .Select(question => (int?)question.OrderIndex)
            .FirstOrDefaultAsync(cancellationToken);

        if (questionIndex is null)
        {
            return Result.Failure<QuestionResultsResponse>(GameErrors.NotCurrentQuestion);
        }

        QuestionResultsResponse? results = await QuestionResultsBuilder.BuildAsync(
            dbContext, query.GameId, query.QuestionId, questionIndex.Value, cancellationToken);

        return results is null
            ? Result.Failure<QuestionResultsResponse>(GameErrors.NotCurrentQuestion)
            : Result.Success(results);
    }
}
