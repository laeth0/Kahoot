using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Persistence;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Auth;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Microsoft.EntityFrameworkCore;

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
        if (!_currentUser.UserId.HasValue)
        {
            return Result.Failure<QuizSummaryResponse>(AuthErrors.Unauthorized);
        }

        Guid hostAccountId = _currentUser.UserId.Value;

        Quiz? quiz = await _dbContext.Quizzes
            .TagWith("Quizzes:UpdateQuiz")
            .Where(q => q.Id == request.QuizId && q.HostAccountId == hostAccountId)
            .SingleOrDefaultAsync(cancellationToken);

        if (quiz is null)
        {
            return Result.Failure<QuizSummaryResponse>(QuizErrors.NotFound);
        }

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

        string trimmedTitle = request.Title.Trim();
        string? trimmedDescription = string.IsNullOrWhiteSpace(request.Description)
            ? null
            : request.Description.Trim();

        quiz.Title = trimmedTitle;
        quiz.Description = trimmedDescription;
        quiz.IsPublished = false;
        quiz.Revision++;

        await _dbContext.SaveChangesAsync(cancellationToken);

        int questionCount = await _dbContext.Questions
            .TagWith("Quizzes:GetQuestionCount")
            .CountAsync(q => q.QuizId == quiz.Id && q.HostAccountId == hostAccountId, cancellationToken);

        QuizSummaryResponse response = new QuizSummaryResponse(
            quiz.Id,
            quiz.Title,
            quiz.Description,
            quiz.IsPublished,
            quiz.Revision,
            questionCount,
            quiz.CreatedAt,
            quiz.UpdatedAt);

        return Result.Success(response);
    }
}
