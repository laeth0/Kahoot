using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Errors;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Security;
using Kahoot.Application.Quizzes.Common;
using Kahoot.Domain.Common;
using Kahoot.Domain.Games;
using Kahoot.Domain.Quizzes;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Application.Quizzes.UpdateQuiz;

internal sealed class UpdateQuizCommandHandler(IApplicationDbContext dbContext, ICurrentUser currentUser)
    : ICommandHandler<UpdateQuizCommand>
{
    public async Task<Result> Handle(UpdateQuizCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.HostId is not { } hostId)
        {
            return Result.Failure(SharedErrors.Unauthorized);
        }

        Quiz? quiz = await dbContext.Quizzes
            .FirstOrDefaultAsync(candidate => candidate.Id == command.QuizId && candidate.HostId == hostId, cancellationToken);
        if (quiz is null)
        {
            return Result.Failure(QuizErrors.NotFound);
        }

        bool inUse = await dbContext.GameSessions
            .AnyAsync(session => session.QuizId == quiz.Id && session.Status != GameStatus.Finished, cancellationToken);
        if (inUse)
        {
            return Result.Failure(QuizErrors.InUse);
        }

        quiz.Title = command.Title.Trim();
        quiz.Description = string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim();
        quiz.IsPublished = false;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
