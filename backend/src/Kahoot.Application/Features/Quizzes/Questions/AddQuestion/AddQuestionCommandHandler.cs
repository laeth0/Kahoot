using Kahoot.Application.Common.Exceptions;
using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Persistence;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Auth;
using Kahoot.Application.Features.Quizzes.Questions;
using Kahoot.Domain.Entities;
using Kahoot.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Kahoot.Application.Features.Quizzes.Questions.AddQuestion;

public sealed class AddQuestionCommandHandler : ICommandHandler<AddQuestionCommand, QuestionResponse>
{
    private const string ImageOwnershipConstraintName = "ix_questions_image_id_host_account_id";
    private const string ImageReferenceConstraintName = "fk_questions_question_images_image_id_host_account_id";

    private readonly IAppDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public AddQuestionCommandHandler(
        IAppDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<Result<QuestionResponse>> Handle(
        AddQuestionCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Result.Failure<QuestionResponse>(AuthErrors.Unauthorized);
        }

        Guid hostAccountId = _currentUser.UserId.Value;

        await using IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        User? host = await _dbContext.GetUserForUpdateAsync(hostAccountId, cancellationToken);
        if (host is null || host.Role != UserRole.Host || host.Status != UserStatus.Active)
        {
            return Result.Failure<QuestionResponse>(AuthErrors.Unauthorized);
        }

        Quiz? quiz = await _dbContext.Quizzes
            .TagWith("Quizzes:AddQuestion:FindQuiz")
            .Where(q => q.Id == request.QuizId && q.HostAccountId == hostAccountId)
            .SingleOrDefaultAsync(cancellationToken);

        if (quiz is null)
        {
            return Result.Failure<QuestionResponse>(QuizErrors.NotFound);
        }

        bool hasActiveGameSession = await _dbContext.Games
            .TagWith("Quizzes:AddQuestion:CheckActiveGameSession")
            .AnyAsync(game => game.SourceQuizId == quiz.Id &&
                              game.HostAccountId == hostAccountId &&
                              game.Status != GameStatus.Finished,
                      cancellationToken);

        if (hasActiveGameSession)
        {
            return Result.Failure<QuestionResponse>(QuizErrors.InUse);
        }

        int currentQuestionCount = await _dbContext.Questions
            .TagWith("Quizzes:AddQuestion:GetQuestionCount")
            .CountAsync(q => q.QuizId == quiz.Id && q.HostAccountId == hostAccountId, cancellationToken);

        if (currentQuestionCount >= 200)
        {
            return Result.Failure<QuestionResponse>(QuizErrors.QuestionLimitExceeded);
        }

        string? imageUrl = null;

        if (request.ImageId.HasValue)
        {
            QuestionImage? image = await _dbContext.QuestionImages
                .TagWith("Quizzes:AddQuestion:ValidateImage")
                .Where(candidate => candidate.Id == request.ImageId.Value && candidate.HostAccountId == hostAccountId)
                .SingleOrDefaultAsync(cancellationToken);

            if (image is null || await _dbContext.Questions
                    .AnyAsync(existing => existing.ImageId == image.Id, cancellationToken))
            {
                return Result.Failure<QuestionResponse>(QuizErrors.InvalidImageReference);
            }

            imageUrl = image.StoragePath;
            image.UnreferencedSince = null;
        }

        Question question = new Question
        {
            Id = Guid.NewGuid(),
            HostAccountId = hostAccountId,
            QuizId = quiz.Id,
            Text = request.Text.Trim(),
            ImageId = request.ImageId,
            DurationSeconds = request.DurationSeconds,
            BasePoints = request.BasePoints,
            OrderIndex = currentQuestionCount
        };

        List<Choice> choices = new List<Choice>(request.Choices.Count);
        List<ChoiceResponse> choiceResponses = new List<ChoiceResponse>(request.Choices.Count);

        for (int i = 0; i < request.Choices.Count; i++)
        {
            ChoiceRequest choiceRequest = request.Choices[i];
            Choice choice = new Choice
            {
                Id = Guid.NewGuid(),
                HostAccountId = hostAccountId,
                QuestionId = question.Id,
                Text = choiceRequest.Text.Trim(),
                IsCorrect = choiceRequest.IsCorrect,
                OrderIndex = i
            };

            choices.Add(choice);
            choiceResponses.Add(new ChoiceResponse(
                choice.Id,
                choice.OrderIndex,
                choice.Text,
                choice.IsCorrect));
        }

        quiz.Revision++;

        _dbContext.Questions.Add(question);
        _dbContext.Choices.AddRange(choices);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException exception) when (
            string.Equals(exception.ConstraintName, ImageOwnershipConstraintName, StringComparison.Ordinal))
        {
            return Result.Failure<QuestionResponse>(QuizErrors.InvalidImageReference);
        }
        catch (ForeignKeyConstraintViolationException exception) when (
            string.Equals(exception.ConstraintName, ImageReferenceConstraintName, StringComparison.Ordinal))
        {
            return Result.Failure<QuestionResponse>(QuizErrors.InvalidImageReference);
        }
        catch (DbUpdateConcurrencyException exception) when (
            exception.Entries.Any(entry => entry.Entity is QuestionImage))
        {
            return Result.Failure<QuestionResponse>(QuizErrors.InvalidImageReference);
        }

        await transaction.CommitAsync(cancellationToken);

        QuestionResponse response = new QuestionResponse(
            question.Id,
            question.QuizId,
            question.OrderIndex,
            question.Text,
            question.ImageId,
            imageUrl,
            question.DurationSeconds,
            question.BasePoints,
            choiceResponses);

        return Result.Success(response);
    }
}
