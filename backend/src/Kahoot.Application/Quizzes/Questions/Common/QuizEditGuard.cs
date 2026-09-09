using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Errors;
using Kahoot.Application.Common.Security;
using Kahoot.Application.Quizzes.Common;
using Kahoot.Domain.Common;
using Kahoot.Domain.Games;
using Kahoot.Domain.Quizzes;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Application.Quizzes.Questions.Common;

internal static class QuizEditGuard
{
    public static async Task<Result<Quiz>> LoadEditableQuizAsync(
        IApplicationDbContext dbContext,
        ICurrentUser currentUser,
        Guid quizId,
        CancellationToken cancellationToken)
    {
        if (currentUser.HostId is not { } hostId)
        {
            return Result.Failure<Quiz>(SharedErrors.Unauthorized);
        }

        Quiz? quiz = await dbContext.Quizzes
            .FirstOrDefaultAsync(candidate => candidate.Id == quizId && candidate.HostId == hostId, cancellationToken);
        if (quiz is null)
        {
            return Result.Failure<Quiz>(QuizErrors.NotFound);
        }

        bool inUse = await dbContext.GameSessions
            .AnyAsync(session => session.QuizId == quiz.Id && session.Status != GameStatus.Finished, cancellationToken);

        return inUse ? Result.Failure<Quiz>(QuizErrors.InUse) : Result.Success(quiz);
    }
}
