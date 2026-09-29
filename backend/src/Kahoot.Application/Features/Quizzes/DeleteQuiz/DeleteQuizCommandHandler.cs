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
        // Tenant Isolation - Authenticates host session before accepting quiz deletion
        if (!_currentUser.UserId.HasValue)
        {
            return Result.Failure(AuthErrors.Unauthorized);
        }

        Guid hostAccountId = _currentUser.UserId.Value;

        // Transactional Consistency Boundary - Serializes host quiz deletion against concurrent game snapshot creation (QUIZ-RISK-004)
        await using IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Serialized Host Mutation Barrier - Acquires SELECT FOR UPDATE on host row to serialize quiz changes
        User? host = await _dbContext.GetUserForUpdateAsync(hostAccountId, cancellationToken);
        if (host is null || host.Role != UserRole.Host || host.Status != UserStatus.Active)
        {
            return Result.Failure(AuthErrors.Unauthorized);
        }

        // Multi-Tenant Isolation - Scopes quiz seek strictly to host tenant boundary
        Quiz? quiz = await _dbContext.Quizzes
            .TagWith("Quizzes:DeleteQuiz:FindQuiz")
            .Where(q => q.Id == request.QuizId && q.HostAccountId == hostAccountId)
            .SingleOrDefaultAsync(cancellationToken);

        if (quiz is null)
        {
            return Result.Failure(QuizErrors.NotFound);
        }

        // Active & Historical Session Scan - Evaluates game statuses associated with source quiz
        List<GameStatus> gameStatuses = await _dbContext.Games
            .AsNoTracking()
            .TagWith("Quizzes:DeleteQuiz:GetGameStatuses")
            .Where(game => game.SourceQuizId == quiz.Id && game.HostAccountId == hostAccountId)
            .Select(game => game.Status)
            .ToListAsync(cancellationToken);

        // Active Session Lock (QUIZ-ERR-006, QUIZ-RISK-001) - Blocks deletion if an active game session is running
        if (gameStatuses.Any(status => status != GameStatus.Finished))
        {
            return Result.Failure(QuizErrors.InUse);
        }

        // Ever-Played Invariant (QUIZ-DEL-001, QUIZ-ERR-007) - Rejects deletion if quiz has ever launched a game to protect history
        if (gameStatuses.Count > 0)
        {
            return Result.Failure(QuizErrors.HasSessions);
        }

        // Orphan Image Lifecycle Handoff (IMG-LIFE-001) - Marks unattached images with UnreferencedSince = NOW() for 7-day reclamation sweep
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

        // Never-Played Deletion - Permanently deletes unplayed quiz entity and cascades to questions and choices
        _dbContext.Quizzes.Remove(quiz);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
