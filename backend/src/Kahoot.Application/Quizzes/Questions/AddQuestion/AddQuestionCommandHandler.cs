using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Security;
using Kahoot.Application.Quizzes.Common;
using Kahoot.Application.Quizzes.Questions.Common;
using Kahoot.Domain.Common;
using Kahoot.Domain.Quizzes;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Application.Quizzes.Questions.AddQuestion;

internal sealed class AddQuestionCommandHandler(
    IApplicationDbContext dbContext,
    ICurrentUser currentUser,
    IDbExceptionInterpreter dbExceptionInterpreter) : ICommandHandler<AddQuestionCommand, Guid>
{
    public async Task<Result<Guid>> Handle(AddQuestionCommand command, CancellationToken cancellationToken)
    {
        Result<Quiz> quizResult = await QuizEditGuard.LoadEditableQuizAsync(
            dbContext, currentUser, command.QuizId, cancellationToken);
        if (quizResult.IsFailure)
        {
            return Result.Failure<Guid>(quizResult.Error);
        }

        Quiz quiz = quizResult.Value;

        int nextOrderIndex = await dbContext.Questions
            .Where(question => question.QuizId == quiz.Id)
            .Select(question => (int?)question.OrderIndex)
            .MaxAsync(cancellationToken) + 1 ?? 0;

        Question question = new()
        {
            QuizId = quiz.Id,
            OrderIndex = nextOrderIndex,
            Text = command.Text.Trim(),
            ImageUrl = QuestionMapping.Normalize(command.ImageUrl),
            TimeLimitSeconds = command.TimeLimitSeconds,
            Points = command.Points
        };
        question.Choices = QuestionMapping.BuildChoices(question.Id, command.Choices);

        quiz.IsPublished = false;
        dbContext.Questions.Add(question);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (dbExceptionInterpreter.IsUniqueViolation(exception, "uq_question_quiz_order"))
        {
            return Result.Failure<Guid>(QuizErrors.ConcurrentModification);
        }

        return Result.Success(question.Id);
    }
}
