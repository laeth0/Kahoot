using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Persistence;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Auth;
using Kahoot.Domain.Entities;

namespace Kahoot.Application.Features.Quizzes.CreateQuiz;

public sealed class CreateQuizCommandHandler : ICommandHandler<CreateQuizCommand, QuizSummaryResponse>
{
    private readonly IAppDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;

    public CreateQuizCommandHandler(
        IAppDbContext dbContext,
        ICurrentUser currentUser,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<Result<QuizSummaryResponse>> Handle(
        CreateQuizCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Result.Failure<QuizSummaryResponse>(AuthErrors.Unauthorized);
        }

        Guid hostAccountId = _currentUser.UserId.Value;
        DateTimeOffset utcNow = _timeProvider.GetUtcNow();

        string trimmedTitle = request.Title.Trim();
        string? trimmedDescription = string.IsNullOrWhiteSpace(request.Description)
            ? null
            : request.Description.Trim();

        Quiz quiz = new Quiz
        {
            Id = Guid.NewGuid(),
            HostAccountId = hostAccountId,
            Title = trimmedTitle,
            Description = trimmedDescription,
            IsPublished = false,
            Revision = 1,
            CreatedAt = utcNow,
            UpdatedAt = utcNow,
            CreatedBy = hostAccountId,
            UpdatedBy = hostAccountId
        };

        _dbContext.Quizzes.Add(quiz);
        await _dbContext.SaveChangesAsync(cancellationToken);

        QuizSummaryResponse response = new QuizSummaryResponse(
            quiz.Id,
            quiz.Title,
            quiz.Description,
            quiz.IsPublished,
            quiz.Revision,
            QuestionCount: 0,
            quiz.CreatedAt,
            quiz.UpdatedAt);

        return Result.Success(response);
    }
}
