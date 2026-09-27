using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Persistence;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Auth;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Kahoot.Application.Features.Quizzes.ReorderQuestions;

public sealed class ReorderQuestionsCommandHandler : ICommandHandler<ReorderQuestionsCommand, ReorderQuestionsResponse>
{
    private readonly IAppDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public ReorderQuestionsCommandHandler(
        IAppDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<Result<ReorderQuestionsResponse>> Handle(
        ReorderQuestionsCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Result.Failure<ReorderQuestionsResponse>(AuthErrors.Unauthorized);
        }

        Guid hostAccountId = _currentUser.UserId.Value;

        Quiz? quiz = await _dbContext.Quizzes
            .TagWith("Quizzes:Reorder:FindQuiz")
            .Where(q => q.Id == request.QuizId && q.HostAccountId == hostAccountId)
            .SingleOrDefaultAsync(cancellationToken);

        if (quiz is null)
        {
            return Result.Failure<ReorderQuestionsResponse>(QuizErrors.NotFound);
        }

        bool hasActiveGameSession = await _dbContext.Games
            .TagWith("Quizzes:Reorder:CheckActiveGameSession")
            .AnyAsync(game => game.SourceQuizId == quiz.Id &&
                              game.HostAccountId == hostAccountId &&
                              game.Status != GameStatus.Finished,
                      cancellationToken);

        if (hasActiveGameSession)
        {
            return Result.Failure<ReorderQuestionsResponse>(QuizErrors.InUse);
        }

        if (request.QuestionIds.Distinct().Count() != request.QuestionIds.Count)
        {
            return Result.Failure<ReorderQuestionsResponse>(QuizErrors.QuestionSetMismatch);
        }

        List<Guid> currentQuestionIds = await _dbContext.Questions
            .TagWith("Quizzes:Reorder:GetCurrentQuestionIds")
            .Where(q => q.QuizId == quiz.Id && q.HostAccountId == hostAccountId)
            .Select(q => q.Id)
            .ToListAsync(cancellationToken);

        if (currentQuestionIds.Count != request.QuestionIds.Count)
        {
            return Result.Failure<ReorderQuestionsResponse>(QuizErrors.QuestionSetMismatch);
        }

        HashSet<Guid> currentQuestionIdSet = currentQuestionIds.ToHashSet();
        if (!request.QuestionIds.All(id => currentQuestionIdSet.Contains(id)))
        {
            return Result.Failure<ReorderQuestionsResponse>(QuizErrors.QuestionSetMismatch);
        }

        await using IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        for (int i = 0; i < request.QuestionIds.Count; i++)
        {
            Guid questionId = request.QuestionIds[i];
            int tempIndex = -(i + 1);

            await _dbContext.Questions
                .Where(q => q.Id == questionId && q.QuizId == quiz.Id && q.HostAccountId == hostAccountId)
                .ExecuteUpdateAsync(setter => setter.SetProperty(q => q.OrderIndex, tempIndex), cancellationToken);
        }

        await _dbContext.Questions
            .TagWith("Quizzes:Reorder:FinalizeOrder")
            .Where(q => q.QuizId == quiz.Id && q.HostAccountId == hostAccountId && q.OrderIndex < 0)
            .ExecuteUpdateAsync(setter => setter.SetProperty(q => q.OrderIndex, q => (-q.OrderIndex) - 1), cancellationToken);

        quiz.IsPublished = false;
        quiz.Revision++;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        ReorderQuestionsResponse response = new ReorderQuestionsResponse(
            "Questions reordered successfully.",
            quiz.Revision);

        return Result.Success(response);
    }
}
