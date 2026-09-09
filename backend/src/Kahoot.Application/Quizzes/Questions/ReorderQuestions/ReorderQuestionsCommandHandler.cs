using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Security;
using Kahoot.Application.Quizzes.Common;
using Kahoot.Application.Quizzes.Questions.Common;
using Kahoot.Domain.Common;
using Kahoot.Domain.Quizzes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Kahoot.Application.Quizzes.Questions.ReorderQuestions;

internal sealed class ReorderQuestionsCommandHandler(IApplicationDbContext dbContext, ICurrentUser currentUser)
    : ICommandHandler<ReorderQuestionsCommand>
{
    private const int TemporaryOffset = 1_000_000;

    public async Task<Result> Handle(ReorderQuestionsCommand command, CancellationToken cancellationToken)
    {
        Result<Quiz> quizResult = await QuizEditGuard.LoadEditableQuizAsync(
            dbContext, currentUser, command.QuizId, cancellationToken);
        if (quizResult.IsFailure)
        {
            return Result.Failure(quizResult.Error);
        }

        List<Question> questions = await dbContext.Questions
            .Where(question => question.QuizId == command.QuizId)
            .ToListAsync(cancellationToken);

        HashSet<Guid> requested = [.. command.OrderedQuestionIds];
        if (requested.Count != questions.Count || !questions.All(question => requested.Contains(question.Id)))
        {
            return Result.Failure(QuizErrors.QuestionSetMismatch);
        }

        Dictionary<Guid, int> targetIndex = command.OrderedQuestionIds
            .Select((id, index) => (id, index))
            .ToDictionary(pair => pair.id, pair => pair.index);

        await using IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        foreach (Question question in questions)
        {
            question.OrderIndex = targetIndex[question.Id] + TemporaryOffset;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (Question question in questions)
        {
            question.OrderIndex = targetIndex[question.Id];
        }

        quizResult.Value.IsPublished = false;
        await dbContext.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
