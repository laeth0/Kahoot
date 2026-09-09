using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Errors;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Security;
using Kahoot.Application.Quizzes.Common;
using Kahoot.Domain.Common;
using Kahoot.Domain.Quizzes;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Application.Quizzes.DeleteQuiz;

internal sealed class DeleteQuizCommandHandler(IApplicationDbContext dbContext, ICurrentUser currentUser)
    : ICommandHandler<DeleteQuizCommand>
{
    public async Task<Result> Handle(DeleteQuizCommand command, CancellationToken cancellationToken)
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

        bool hasSessions = await dbContext.GameSessions
            .AnyAsync(session => session.QuizId == quiz.Id, cancellationToken);
        if (hasSessions)
        {
            return Result.Failure(QuizErrors.HasSessions);
        }

        dbContext.Quizzes.Remove(quiz);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
