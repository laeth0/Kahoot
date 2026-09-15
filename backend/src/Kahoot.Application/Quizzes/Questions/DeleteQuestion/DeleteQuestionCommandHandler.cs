using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Security;
using Kahoot.Application.Games.Presence;
using Kahoot.Application.Quizzes.Common;
using Kahoot.Application.Quizzes.Questions.Common;
using Kahoot.Domain.Common;
using Kahoot.Domain.Quizzes;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Application.Quizzes.Questions.DeleteQuestion;

internal sealed class DeleteQuestionCommandHandler(
    IApplicationDbContext dbContext,
    ICurrentUser currentUser,
    IHostPresenceTracker hostPresenceTracker,
    TimeProvider timeProvider)
    : ICommandHandler<DeleteQuestionCommand>
{
    private const int TemporaryOffset = 1_000_000;

    public async Task<Result> Handle(DeleteQuestionCommand command, CancellationToken cancellationToken)
    {
        Result<Quiz> quizResult = await QuizEditGuard.LoadEditableQuizAsync(
            dbContext, currentUser, hostPresenceTracker, timeProvider, command.QuizId, cancellationToken);
        if (quizResult.IsFailure)
        {
            return Result.Failure(quizResult.Error);
        }

        Question? question = await dbContext.Questions
            .FirstOrDefaultAsync(
                candidate => candidate.Id == command.QuestionId && candidate.QuizId == command.QuizId,
                cancellationToken);
        if (question is null)
        {
            return Result.Failure(QuizErrors.QuestionNotFound);
        }

        int deletedOrderIndex = question.OrderIndex;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        dbContext.Questions.Remove(question);
        quizResult.Value.IsPublished = false;
        await dbContext.SaveChangesAsync(cancellationToken);

        await dbContext.Questions
            .Where(candidate => candidate.QuizId == command.QuizId && candidate.OrderIndex > deletedOrderIndex)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    candidate => candidate.OrderIndex,
                    candidate => candidate.OrderIndex + TemporaryOffset),
                cancellationToken);

        await dbContext.Questions
            .Where(candidate =>
                candidate.QuizId == command.QuizId &&
                candidate.OrderIndex > deletedOrderIndex + TemporaryOffset)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    candidate => candidate.OrderIndex,
                    candidate => candidate.OrderIndex - TemporaryOffset - 1),
                cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
