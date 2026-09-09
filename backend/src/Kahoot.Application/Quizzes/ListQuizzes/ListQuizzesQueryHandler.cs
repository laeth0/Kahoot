using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Errors;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Security;
using Kahoot.Application.Quizzes.Common;
using Kahoot.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Application.Quizzes.ListQuizzes;

internal sealed class ListQuizzesQueryHandler(IApplicationDbContext dbContext, ICurrentUser currentUser)
    : IQueryHandler<ListQuizzesQuery, IReadOnlyList<QuizSummaryResponse>>
{
    public async Task<Result<IReadOnlyList<QuizSummaryResponse>>> Handle(
        ListQuizzesQuery query,
        CancellationToken cancellationToken)
    {
        if (currentUser.HostId is not { } hostId)
        {
            return Result.Failure<IReadOnlyList<QuizSummaryResponse>>(SharedErrors.Unauthorized);
        }

        List<QuizSummaryResponse> quizzes = await dbContext.Quizzes
            .AsNoTracking()
            .Where(quiz => quiz.HostId == hostId)
            .OrderByDescending(quiz => quiz.Id)
            .Select(quiz => new QuizSummaryResponse(
                quiz.Id,
                quiz.Title,
                quiz.Description,
                quiz.IsPublished,
                quiz.Questions.Count))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<QuizSummaryResponse>>(quizzes);
    }
}
