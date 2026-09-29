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
        // Tenant Isolation - Authenticates host session before accepting question deletion
        if (!_currentUser.UserId.HasValue)
        {
            return Result.Failure(AuthErrors.Unauthorized);
        }

        Guid hostAccountId = _currentUser.UserId.Value;

        // Transactional Consistency Boundary - Serializes host quiz modifications against concurrent game snapshots (QUIZ-RISK-004)
        await using IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Serialized Host Mutation Barrier - Acquires SELECT FOR UPDATE on host row to serialize question mutation
        User? host = await _dbContext.GetUserForUpdateAsync(hostAccountId, cancellationToken);
        if (host is null || host.Role != UserRole.Host || host.Status != UserStatus.Active)
        {
            return Result.Failure(AuthErrors.Unauthorized);
        }

        // Multi-Tenant Isolation - Scopes quiz seek strictly to host tenant boundary
        Quiz? quiz = await _dbContext.Quizzes
            .TagWith("Quizzes:DeleteQuestion:FindQuiz")
            .Where(q => q.Id == request.QuizId && q.HostAccountId == hostAccountId)
            .SingleOrDefaultAsync(cancellationToken);

        if (quiz is null)
        {
            return Result.Failure(QuizErrors.NotFound);
        }

        // Active Session Lock (QUIZ-ERR-006, QUIZ-RISK-001) - Blocks question deletion when active games are referencing the quiz
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

        // Multi-Tenant Question Seek - Queries target question strictly scoped by quiz ID and host account ID
        Question? question = await _dbContext.Questions
            .TagWith("Quizzes:DeleteQuestion:FindQuestion")
            .Where(q => q.Id == request.QuestionId && q.QuizId == quiz.Id && q.HostAccountId == hostAccountId)
            .SingleOrDefaultAsync(cancellationToken);

        if (question is null)
        {
            return Result.Failure(QuizErrors.QuestionNotFound);
        }

        int deletedOrderIndex = question.OrderIndex;

        // Cascade Entity Deletion - Queues question removal, cascading deletion to dependent choices via FK constraint
        _dbContext.Questions.Remove(question);

        // Detached Image Orphan Marking (IMG-LIFE-001) - Identifies image detachment caused by question deletion
        if (question.ImageId.HasValue)
        {
            QuestionImage? image = await _dbContext.QuestionImages
                .TagWith("Quizzes:DeleteQuestion:GetImage")
                .Where(candidate => candidate.Id == question.ImageId.Value && candidate.HostAccountId == hostAccountId)
                .SingleOrDefaultAsync(cancellationToken);

            // Historical Snapshot Guard - Sets unreferenced timestamp only if no historical game session snapshot retains the image
            if (image is not null &&
                !await _dbContext.GameQuestionSnapshots
                    .AnyAsync(snapshot => snapshot.ImageId == image.Id, cancellationToken))
            {
                image.UnreferencedSince = _timeProvider.GetUtcNow();
            }
        }

        // Monotonic Revision Increment - Invalidates cached quiz representations and advances optimistic concurrency revision
        quiz.Revision++;

        // Intermediate Persistence - Flushes question removal to database before executing bulk index reorganization
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Two-Phase Re-indexing (Phase 1: Negative Staging) - Inverts OrderIndex to negative values to prevent unique constraint violations on (quiz_id, order_index)
        await _dbContext.Questions
            .TagWith("Quizzes:DeleteQuestion:ShiftNegative")
            .Where(q => q.QuizId == quiz.Id && q.HostAccountId == hostAccountId && q.OrderIndex > deletedOrderIndex)
            .ExecuteUpdateAsync(setter => setter.SetProperty(q => q.OrderIndex, q => -q.OrderIndex), cancellationToken);

        // Two-Phase Re-indexing (Phase 2: Bulk Compact) - Shifts subsequent questions downward to restore contiguous zero-based indices: (-OrderIndex) - 1
        await _dbContext.Questions
            .TagWith("Quizzes:DeleteQuestion:ShiftDecrement")
            .Where(q => q.QuizId == quiz.Id && q.HostAccountId == hostAccountId && q.OrderIndex < 0)
            .ExecuteUpdateAsync(setter => setter.SetProperty(q => q.OrderIndex, q => (-q.OrderIndex) - 1), cancellationToken);

        // Atomic Transaction Commit - Commits question deletion, image status change, revision advance, and re-indexing atomically
        await transaction.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
