using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Persistence;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Auth;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Kahoot.Application.Features.Quizzes.UpdateQuiz;

public sealed class UpdateQuizCommandHandler : ICommandHandler<UpdateQuizCommand, QuizSummaryResponse>
{
    private readonly IAppDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public UpdateQuizCommandHandler(
        IAppDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<Result<QuizSummaryResponse>> Handle(
        UpdateQuizCommand request,
        CancellationToken cancellationToken)
    {
        // Tenant Isolation - Authenticates host session before accepting quiz update
        if (!_currentUser.UserId.HasValue)
        {
            return Result.Failure<QuizSummaryResponse>(AuthErrors.Unauthorized);
        }

        Guid hostAccountId = _currentUser.UserId.Value;

        // Transactional Consistency Boundary - Serializes host quiz modifications against concurrent game snapshots (QUIZ-RISK-004)
        await using IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Serialized Host Mutation Barrier - Acquires SELECT FOR UPDATE on host row to serialize quiz changes
        User? host = await _dbContext.GetUserForUpdateAsync(hostAccountId, cancellationToken);
        if (host is null || host.Role != UserRole.Host || host.Status != UserStatus.Active)
        {
            return Result.Failure<QuizSummaryResponse>(AuthErrors.Unauthorized);
        }

        // Multi-Tenant Isolation - Scopes quiz seek strictly to host tenant boundary
        Quiz? quiz = await _dbContext.Quizzes
            .TagWith("Quizzes:UpdateQuiz")
            .Where(q => q.Id == request.QuizId && q.HostAccountId == hostAccountId)
            .SingleOrDefaultAsync(cancellationToken);

        if (quiz is null)
        {
            return Result.Failure<QuizSummaryResponse>(QuizErrors.NotFound);
        }

        // Active Session Lock (QUIZ-ERR-006, QUIZ-RISK-001) - Prevents editing quiz while a live game session is active
        bool hasActiveGameSession = await _dbContext.Games
            .TagWith("Quizzes:CheckActiveGameSession")
            .AnyAsync(game => game.SourceQuizId == quiz.Id &&
                              game.HostAccountId == hostAccountId &&
                              game.Status != GameStatus.Finished,
                      cancellationToken);

        if (hasActiveGameSession)
        {
            return Result.Failure<QuizSummaryResponse>(QuizErrors.InUse);
        }

        // Plain-Text Content Sanitization (QUIZ-SEC-002) - Trims scalar text to prevent whitespace padding
        string trimmedTitle = request.Title.Trim();
        string? trimmedDescription = string.IsNullOrWhiteSpace(request.Description)
            ? null
            : request.Description.Trim();

        quiz.Title = trimmedTitle;
        quiz.Description = trimmedDescription;
        // Monotonic Revision Increment (QUIZ-AUTH-002) - Advances revision to invalidate stale client views and OCC fences
        quiz.Revision++;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        // Question Count Aggregation - Queries question count for updated summary DTO
        int questionCount = await _dbContext.Questions
            .TagWith("Quizzes:GetQuestionCount")
            .CountAsync(q => q.QuizId == quiz.Id && q.HostAccountId == hostAccountId, cancellationToken);

        QuizSummaryResponse response = new QuizSummaryResponse(
            quiz.Id,
            quiz.Title,
            quiz.Description,
            quiz.Revision,
            questionCount,
            quiz.CreatedAt,
            quiz.UpdatedAt);

        return Result.Success(response);
    }
}
