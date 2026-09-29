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
        // Tenant Isolation - Authenticates host session before accepting reorder command
        if (!_currentUser.UserId.HasValue)
        {
            return Result.Failure<ReorderQuestionsResponse>(AuthErrors.Unauthorized);
        }

        Guid hostAccountId = _currentUser.UserId.Value;

        // Transactional Consistency Boundary - Serializes host quiz modifications against concurrent game snapshots (QUIZ-RISK-004)
        await using IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Serialized Host Mutation Barrier - Acquires SELECT FOR UPDATE on host row to serialize quiz changes
        User? host = await _dbContext.GetUserForUpdateAsync(hostAccountId, cancellationToken);
        if (host is null || host.Role != UserRole.Host || host.Status != UserStatus.Active)
        {
            return Result.Failure<ReorderQuestionsResponse>(AuthErrors.Unauthorized);
        }

        // Multi-Tenant Isolation - Scopes quiz lookup strictly to host tenant boundary
        Quiz? quiz = await _dbContext.Quizzes
            .TagWith("Quizzes:Reorder:FindQuiz")
            .Where(q => q.Id == request.QuizId && q.HostAccountId == hostAccountId)
            .SingleOrDefaultAsync(cancellationToken);

        if (quiz is null)
        {
            return Result.Failure<ReorderQuestionsResponse>(QuizErrors.NotFound);
        }

        // Active Session Lock (QUIZ-ERR-006, QUIZ-RISK-001) - Prevents question reordering while active games are running
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

        // Permutation Invariant Validation (QUIZ-REORDER-001, QUIZ-ERR-002) - Rejects duplicate question IDs in payload
        if (request.QuestionIds.Distinct().Count() != request.QuestionIds.Count)
        {
            return Result.Failure<ReorderQuestionsResponse>(QuizErrors.QuestionSetMismatch);
        }

        // Current Question Index Seek - Retrieves all existing question IDs belonging to this quiz
        List<Guid> currentQuestionIds = await _dbContext.Questions
            .TagWith("Quizzes:Reorder:GetCurrentQuestionIds")
            .Where(q => q.QuizId == quiz.Id && q.HostAccountId == hostAccountId)
            .Select(q => q.Id)
            .ToListAsync(cancellationToken);

        // Bijection Set Check - Guarantees payload has exact same count and membership as current question roster
        if (currentQuestionIds.Count != request.QuestionIds.Count)
        {
            return Result.Failure<ReorderQuestionsResponse>(QuizErrors.QuestionSetMismatch);
        }

        HashSet<Guid> currentQuestionIdSet = currentQuestionIds.ToHashSet();
        if (!request.QuestionIds.All(id => currentQuestionIdSet.Contains(id)))
        {
            return Result.Failure<ReorderQuestionsResponse>(QuizErrors.QuestionSetMismatch);
        }

        // Two-Phase Reorder Swap (Phase 1: Negative Staging) - Maps to negative indices to prevent unique constraint collisions on OrderIndex
        for (int i = 0; i < request.QuestionIds.Count; i++)
        {
            Guid questionId = request.QuestionIds[i];
            int tempIndex = -(i + 1);

            await _dbContext.Questions
                .Where(q => q.Id == questionId && q.QuizId == quiz.Id && q.HostAccountId == hostAccountId)
                .ExecuteUpdateAsync(setter => setter.SetProperty(q => q.OrderIndex, tempIndex), cancellationToken);
        }

        // Two-Phase Reorder Swap (Phase 2: Final Offset Inversion) - Flips negative indices back to contiguous 0-based sequence in single bulk query
        await _dbContext.Questions
            .TagWith("Quizzes:Reorder:FinalizeOrder")
            .Where(q => q.QuizId == quiz.Id && q.HostAccountId == hostAccountId && q.OrderIndex < 0)
            .ExecuteUpdateAsync(setter => setter.SetProperty(q => q.OrderIndex, q => (-q.OrderIndex) - 1), cancellationToken);

        // Monotonic Revision Increment (QUIZ-AUTH-002) - Advances revision to invalidate stale client views and OCC fences
        quiz.Revision++;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        ReorderQuestionsResponse response = new ReorderQuestionsResponse(
            "Questions reordered successfully.",
            quiz.Revision);

        return Result.Success(response);
    }
}
