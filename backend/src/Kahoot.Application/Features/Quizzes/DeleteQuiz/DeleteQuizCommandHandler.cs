using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Persistence;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Auth;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Kahoot.Application.Features.Quizzes.DeleteQuiz;

public sealed class DeleteQuizCommandHandler : ICommandHandler<DeleteQuizCommand>
{
    private readonly IAppDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;

    public DeleteQuizCommandHandler(
        IAppDbContext dbContext,
        ICurrentUser currentUser,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<Result> Handle(
        DeleteQuizCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Result.Failure(AuthErrors.Unauthorized);
        }

        Guid hostAccountId = _currentUser.UserId.Value;

        await using IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        User? host = await _dbContext.GetUserForUpdateAsync(hostAccountId, cancellationToken);
        if (host is null || host.Role != UserRole.Host || host.Status != UserStatus.Active)
        {
            return Result.Failure(AuthErrors.Unauthorized);
        }

        Quiz? quiz = await _dbContext.Quizzes
            .TagWith("Quizzes:DeleteQuiz:FindQuiz")
            .Where(q => q.Id == request.QuizId && q.HostAccountId == hostAccountId)
            .SingleOrDefaultAsync(cancellationToken);

        if (quiz is null)
        {
            return Result.Failure(QuizErrors.NotFound);
        }

        List<GameStatus> gameStatuses = await _dbContext.Games
            .AsNoTracking()
            .TagWith("Quizzes:DeleteQuiz:GetGameStatuses")
            .Where(game => game.SourceQuizId == quiz.Id && game.HostAccountId == hostAccountId)
            .Select(game => game.Status)
            .ToListAsync(cancellationToken);

        if (gameStatuses.Any(status => status != GameStatus.Finished))
        {
            return Result.Failure(QuizErrors.InUse);
        }

        if (gameStatuses.Count > 0)
        {
            return Result.Failure(QuizErrors.HasSessions);
        }

        List<Guid> imageIdsInQuiz = await _dbContext.Questions
            .TagWith("Quizzes:DeleteQuiz:GetImageIds")
            .Where(q => q.QuizId == quiz.Id && q.HostAccountId == hostAccountId && q.ImageId.HasValue)
            .Select(q => q.ImageId!.Value)
            .ToListAsync(cancellationToken);

        if (imageIdsInQuiz.Count > 0)
        {
            List<QuestionImage> images = await _dbContext.QuestionImages
                .TagWith("Quizzes:DeleteQuiz:GetImages")
                .Where(image => imageIdsInQuiz.Contains(image.Id) && image.HostAccountId == hostAccountId)
                .ToListAsync(cancellationToken);

            DateTimeOffset utcNow = _timeProvider.GetUtcNow();

            foreach (QuestionImage image in images)
            {
                image.UnreferencedSince = utcNow;
            }
        }

        _dbContext.Quizzes.Remove(quiz);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
