using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Persistence;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Auth;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Kahoot.Application.Features.Quizzes.Questions.DeleteQuestion;

public sealed class DeleteQuestionCommandHandler : ICommandHandler<DeleteQuestionCommand>
{
    private readonly IAppDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;

    public DeleteQuestionCommandHandler(
        IAppDbContext dbContext,
        ICurrentUser currentUser,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<Result> Handle(
        DeleteQuestionCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Result.Failure(AuthErrors.Unauthorized);
        }

        Guid hostAccountId = _currentUser.UserId.Value;

        Quiz? quiz = await _dbContext.Quizzes
            .TagWith("Quizzes:DeleteQuestion:FindQuiz")
            .Where(q => q.Id == request.QuizId && q.HostAccountId == hostAccountId)
            .SingleOrDefaultAsync(cancellationToken);

        if (quiz is null)
        {
            return Result.Failure(QuizErrors.NotFound);
        }

        bool hasActiveGameSession = await _dbContext.Games
            .TagWith("Quizzes:DeleteQuestion:CheckActiveGameSession")
            .AnyAsync(game => game.SourceQuizId == quiz.Id &&
                              game.HostAccountId == hostAccountId &&
                              game.Status != GameStatus.Finished,
                      cancellationToken);

        if (hasActiveGameSession)
        {
            return Result.Failure(QuizErrors.InUse);
        }

        Question? question = await _dbContext.Questions
            .TagWith("Quizzes:DeleteQuestion:FindQuestion")
            .Where(q => q.Id == request.QuestionId && q.QuizId == quiz.Id && q.HostAccountId == hostAccountId)
            .SingleOrDefaultAsync(cancellationToken);

        if (question is null)
        {
            return Result.Failure(QuizErrors.QuestionNotFound);
        }

        int deletedOrderIndex = question.OrderIndex;

        await using IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        _dbContext.Questions.Remove(question);

        if (question.ImageId.HasValue)
        {
            QuestionImage? image = await _dbContext.QuestionImages
                .TagWith("Quizzes:DeleteQuestion:GetImage")
                .Where(candidate => candidate.Id == question.ImageId.Value && candidate.HostAccountId == hostAccountId)
                .SingleOrDefaultAsync(cancellationToken);

            if (image is not null &&
                !await _dbContext.GameQuestionSnapshots
                    .AnyAsync(snapshot => snapshot.ImageId == image.Id, cancellationToken))
            {
                image.UnreferencedSince = _timeProvider.GetUtcNow();
            }
        }

        quiz.IsPublished = false;
        quiz.Revision++;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await _dbContext.Questions
            .TagWith("Quizzes:DeleteQuestion:ShiftNegative")
            .Where(q => q.QuizId == quiz.Id && q.HostAccountId == hostAccountId && q.OrderIndex > deletedOrderIndex)
            .ExecuteUpdateAsync(setter => setter.SetProperty(q => q.OrderIndex, q => -q.OrderIndex), cancellationToken);

        await _dbContext.Questions
            .TagWith("Quizzes:DeleteQuestion:ShiftDecrement")
            .Where(q => q.QuizId == quiz.Id && q.HostAccountId == hostAccountId && q.OrderIndex < 0)
            .ExecuteUpdateAsync(setter => setter.SetProperty(q => q.OrderIndex, q => (-q.OrderIndex) - 1), cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
