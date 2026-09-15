using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Security;
using Kahoot.Application.Games.Presence;
using Kahoot.Application.Quizzes.Questions.Common;
using Kahoot.Domain.Common;
using Kahoot.Domain.Quizzes;

namespace Kahoot.Application.Quizzes.UpdateQuiz;

internal sealed class UpdateQuizCommandHandler(
    IApplicationDbContext dbContext,
    ICurrentUser currentUser,
    IHostPresenceTracker hostPresenceTracker,
    TimeProvider timeProvider)
    : ICommandHandler<UpdateQuizCommand>
{
    public async Task<Result> Handle(UpdateQuizCommand command, CancellationToken cancellationToken)
    {
        Result<Quiz> quizResult = await QuizEditGuard.LoadEditableQuizAsync(
            dbContext, currentUser, hostPresenceTracker, timeProvider, command.QuizId, cancellationToken);
        if (quizResult.IsFailure)
        {
            return Result.Failure(quizResult.Error);
        }

        Quiz quiz = quizResult.Value;
        quiz.Title = command.Title.Trim();
        quiz.Description = string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim();
        quiz.IsPublished = false;

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
