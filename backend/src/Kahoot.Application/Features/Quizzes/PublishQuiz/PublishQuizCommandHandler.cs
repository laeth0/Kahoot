using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Persistence;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Auth;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Application.Features.Quizzes.PublishQuiz;

public sealed class PublishQuizCommandHandler : ICommandHandler<PublishQuizCommand, PublishQuizResponse>
{
    private const int MinQuestionsCount = 1;
    private const int MaxQuestionsCount = 200;
    private const int MinQuestionTextLength = 1;
    private const int MaxQuestionTextLength = 500;
    private const int MinDurationSeconds = 5;
    private const int MaxDurationSeconds = 300;
    private const int MinBasePoints = 0;
    private const int MinChoicesCount = 2;
    private const int MaxChoicesCount = 6;
    private const int MinChoiceTextLength = 1;
    private const int MaxChoiceTextLength = 300;

    private readonly IAppDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;

    public PublishQuizCommandHandler(
        IAppDbContext dbContext,
        ICurrentUser currentUser,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<Result<PublishQuizResponse>> Handle(
        PublishQuizCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Result.Failure<PublishQuizResponse>(AuthErrors.Unauthorized);
        }

        Guid hostAccountId = _currentUser.UserId.Value;

        Quiz? quiz = await _dbContext.Quizzes
            .TagWith("Quizzes:Publish:FindQuiz")
            .Where(q => q.Id == request.QuizId && q.HostAccountId == hostAccountId)
            .SingleOrDefaultAsync(cancellationToken);

        if (quiz is null)
        {
            return Result.Failure<PublishQuizResponse>(QuizErrors.NotFound);
        }

        bool hasActiveGameSession = await _dbContext.Games
            .TagWith("Quizzes:Publish:CheckActiveGameSession")
            .AnyAsync(game => game.SourceQuizId == quiz.Id &&
                              game.HostAccountId == hostAccountId &&
                              game.Status != GameStatus.Finished,
                      cancellationToken);

        if (hasActiveGameSession)
        {
            return Result.Failure<PublishQuizResponse>(QuizErrors.InUse);
        }

        List<Question> questions = await _dbContext.Questions
            .TagWith("Quizzes:Publish:GetQuestions")
            .Where(q => q.QuizId == quiz.Id && q.HostAccountId == hostAccountId)
            .OrderBy(q => q.OrderIndex)
            .ToListAsync(cancellationToken);

        // Checklist Rule 1: Contains between 1 and 200 questions (inclusive)
        if (questions.Count is < MinQuestionsCount or > MaxQuestionsCount)
        {
            return Result.Failure<PublishQuizResponse>(Error.Validation(
                "Validation.Failed",
                $"A published quiz must contain between {MinQuestionsCount} and {MaxQuestionsCount} questions."));
        }

        List<Guid> questionIds = questions.Select(q => q.Id).ToList();
        List<Choice> choices = await _dbContext.Choices
            .TagWith("Quizzes:Publish:GetChoices")
            .Where(c => questionIds.Contains(c.QuestionId) && c.HostAccountId == hostAccountId)
            .OrderBy(c => c.OrderIndex)
            .ToListAsync(cancellationToken);

        ILookup<Guid, Choice> choicesByQuestionId = choices.ToLookup(c => c.QuestionId);

        long totalMaxScore = 0;

        foreach (Question question in questions)
        {
            // Checklist Rule 2: Every question has non-whitespace text (1–500 chars)
            if (string.IsNullOrWhiteSpace(question.Text) || question.Text.Trim().Length is < MinQuestionTextLength or > MaxQuestionTextLength)
            {
                return Result.Failure<PublishQuizResponse>(Error.Validation(
                    "Validation.Failed",
                    $"Question '{question.Id}' text must be non-whitespace and between {MinQuestionTextLength} and {MaxQuestionTextLength} characters."));
            }

            // Checklist Rule 3: Every question has durationSeconds between 5 and 300 seconds
            if (question.DurationSeconds is < MinDurationSeconds or > MaxDurationSeconds)
            {
                return Result.Failure<PublishQuizResponse>(Error.Validation(
                    "Validation.Failed",
                    $"Question '{question.Id}' duration must be between {MinDurationSeconds} and {MaxDurationSeconds} seconds."));
            }

            // Checklist Rule 4: Every question has basePoints between 0 and 2,147,483,647
            if (question.BasePoints < MinBasePoints)
            {
                return Result.Failure<PublishQuizResponse>(Error.Validation(
                    "Validation.Failed",
                    $"Question '{question.Id}' base points must be between {MinBasePoints} and {int.MaxValue}."));
            }

            // Checklist Rule 9: Total potential maximum score does not overflow signed 64-bit integer
            try
            {
                checked
                {
                    totalMaxScore += question.BasePoints;
                }
            }
            catch (OverflowException)
            {
                return Result.Failure<PublishQuizResponse>(Error.Validation(
                    "Validation.Failed",
                    "Total potential maximum score of the quiz exceeds arithmetic safety limits."));
            }

            // Checklist Rule 5: Every question has between 2 and 6 choices (inclusive)
            List<Choice> questionChoices = choicesByQuestionId[question.Id].ToList();
            if (questionChoices.Count is < MinChoicesCount or > MaxChoicesCount)
            {
                return Result.Failure<PublishQuizResponse>(Error.Validation(
                    "Validation.Failed",
                    $"Question '{question.Id}' must contain between {MinChoicesCount} and {MaxChoicesCount} choices."));
            }

            // Checklist Rule 6: Every choice has non-whitespace text (1–300 chars)
            foreach (Choice choice in questionChoices)
            {
                if (string.IsNullOrWhiteSpace(choice.Text) || choice.Text.Trim().Length is < MinChoiceTextLength or > MaxChoiceTextLength)
                {
                    return Result.Failure<PublishQuizResponse>(Error.Validation(
                        "Validation.Failed",
                        $"Choice '{choice.Id}' in question '{question.Id}' text must be non-whitespace and between {MinChoiceTextLength} and {MaxChoiceTextLength} characters."));
                }
            }

            // Checklist Rule 7: Every question has at least 1 choice marked isCorrect = true
            if (!questionChoices.Any(c => c.IsCorrect))
            {
                return Result.Failure<PublishQuizResponse>(Error.Validation(
                    "Validation.Failed",
                    $"Question '{question.Id}' must have at least one choice marked as correct."));
            }
        }

        // Checklist Rule 8: Any referenced mediaId exists, is stored (Status == Active), and belongs to the same Host tenant
        List<Guid> mediaIds = questions
            .Where(q => q.MediaItemId.HasValue)
            .Select(q => q.MediaItemId!.Value)
            .Distinct()
            .ToList();

        if (mediaIds.Count > 0)
        {
            List<MediaItem> activeMediaItems = await _dbContext.MediaItems
                .TagWith("Quizzes:Publish:ValidateMedia")
                .Where(m => mediaIds.Contains(m.Id) && m.HostAccountId == hostAccountId && m.Status == MediaStatus.Active)
                .ToListAsync(cancellationToken);

            if (activeMediaItems.Count != mediaIds.Count)
            {
                return Result.Failure<PublishQuizResponse>(Error.Validation(
                    "Validation.Failed",
                    "One or more referenced media items do not exist, are not active, or belong to another account."));
            }
        }

        // Idempotency: If already published and valid, return 200 OK without bumping revision
        if (!quiz.IsPublished)
        {
            quiz.IsPublished = true;
            quiz.Revision++;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        PublishQuizResponse response = new PublishQuizResponse(
            quiz.Id,
            quiz.IsPublished,
            quiz.Revision,
            quiz.UpdatedAt);

        return Result.Success(response);
    }
}
