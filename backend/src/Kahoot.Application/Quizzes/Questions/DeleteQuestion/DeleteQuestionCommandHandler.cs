using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Security;
using Kahoot.Application.Quizzes.Common;
using Kahoot.Application.Quizzes.Questions.Common;
using Kahoot.Domain.Common;
using Kahoot.Domain.Quizzes;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Application.Quizzes.Questions.DeleteQuestion;

internal sealed class DeleteQuestionCommandHandler(IApplicationDbContext dbContext, ICurrentUser currentUser)
    : ICommandHandler<DeleteQuestionCommand>
{
    public async Task<Result> Handle(DeleteQuestionCommand command, CancellationToken cancellationToken)
    {
        Result<Quiz> quizResult = await QuizEditGuard.LoadEditableQuizAsync(
            dbContext, currentUser, command.QuizId, cancellationToken);
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

        dbContext.Questions.Remove(question);
        quizResult.Value.IsPublished = false;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
