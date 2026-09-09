using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Security;
using Kahoot.Application.Quizzes.Common;
using Kahoot.Application.Quizzes.Questions.Common;
using Kahoot.Domain.Common;
using Kahoot.Domain.Quizzes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Kahoot.Application.Quizzes.Questions.UpdateQuestion;

internal sealed class UpdateQuestionCommandHandler(IApplicationDbContext dbContext, ICurrentUser currentUser)
    : ICommandHandler<UpdateQuestionCommand>
{
    public async Task<Result> Handle(UpdateQuestionCommand command, CancellationToken cancellationToken)
    {
        Result<Quiz> quizResult = await QuizEditGuard.LoadEditableQuizAsync(
            dbContext, currentUser, command.QuizId, cancellationToken);
        if (quizResult.IsFailure)
        {
            return Result.Failure(quizResult.Error);
        }

        Question? question = await dbContext.Questions
            .Include(candidate => candidate.Choices)
            .FirstOrDefaultAsync(
                candidate => candidate.Id == command.QuestionId && candidate.QuizId == command.QuizId,
                cancellationToken);
        if (question is null)
        {
            return Result.Failure(QuizErrors.QuestionNotFound);
        }

        question.Text = command.Text.Trim();
        question.ImageUrl = QuestionMapping.Normalize(command.ImageUrl);
        question.TimeLimitSeconds = command.TimeLimitSeconds;
        question.Points = command.Points;
        quizResult.Value.IsPublished = false;

        await using IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        dbContext.Choices.RemoveRange(question.Choices);
        await dbContext.SaveChangesAsync(cancellationToken);

        question.Choices = QuestionMapping.BuildChoices(question.Id, command.Choices);
        await dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
