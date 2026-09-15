using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Errors;
using Kahoot.Application.Common.Security;
using Kahoot.Application.Games.Presence;
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
        IHostPresenceTracker hostPresenceTracker,
        TimeProvider timeProvider,
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

        List<GameSession> unfinishedSessions = await dbContext.GameSessions
            .Where(session => session.QuizId == quiz.Id && session.Status != GameStatus.Finished)
            .ToListAsync(cancellationToken);

        bool modified = false;
        foreach (GameSession session in unfinishedSessions)
        {
            if (!hostPresenceTracker.IsHostConnectedOrInGracePeriod(session.Id))
            {
                session.Status = GameStatus.Finished;
                session.FinishedAt = timeProvider.GetUtcNow().UtcDateTime;
                hostPresenceTracker.RemoveGame(session.Id);
                modified = true;
            }
        }

        if (modified)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        bool inUse = unfinishedSessions.Any(session => session.Status != GameStatus.Finished);

        return inUse ? Result.Failure<Quiz>(QuizErrors.InUse) : Result.Success(quiz);
    }
}
