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

namespace Kahoot.Application.Features.Quizzes.Questions.UpdateQuestion;

public sealed class UpdateQuestionCommandHandler : ICommandHandler<UpdateQuestionCommand, QuestionResponse>
{
    private const string ImageOwnershipConstraintName = "ix_questions_image_id_host_account_id";
    private const string ImageReferenceConstraintName = "fk_questions_question_images_image_id_host_account_id";

    private readonly IAppDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly TimeProvider _timeProvider;

    public UpdateQuestionCommandHandler(
        IAppDbContext dbContext,
        ICurrentUser currentUser,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _timeProvider = timeProvider;
    }

    public async Task<Result<QuestionResponse>> Handle(
        UpdateQuestionCommand request,
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
            .TagWith("Quizzes:UpdateQuestion:FindQuiz")
            .Where(q => q.Id == request.QuizId && q.HostAccountId == hostAccountId)
            .SingleOrDefaultAsync(cancellationToken);

        if (quiz is null)
        {
            return Result.Failure<QuestionResponse>(QuizErrors.NotFound);
        }

        bool hasActiveGameSession = await _dbContext.Games
            .TagWith("Quizzes:UpdateQuestion:CheckActiveGameSession")
            .AnyAsync(game => game.SourceQuizId == quiz.Id &&
                              game.HostAccountId == hostAccountId &&
                              game.Status != GameStatus.Finished,
                      cancellationToken);

        if (hasActiveGameSession)
        {
            return Result.Failure<QuestionResponse>(QuizErrors.InUse);
        }

        Question? question = await _dbContext.Questions
            .TagWith("Quizzes:UpdateQuestion:FindQuestion")
            .Where(q => q.Id == request.QuestionId && q.QuizId == quiz.Id && q.HostAccountId == hostAccountId)
            .SingleOrDefaultAsync(cancellationToken);

        if (question is null)
        {
            return Result.Failure<QuestionResponse>(QuizErrors.QuestionNotFound);
        }

        Guid? oldImageId = question.ImageId;
        Guid? newImageId = request.ImageId;
        string? imageUrl = null;

        if (oldImageId != newImageId)
        {
            if (newImageId.HasValue)
            {
                QuestionImage? newImage = await _dbContext.QuestionImages
                    .TagWith("Quizzes:UpdateQuestion:ValidateNewImage")
                    .Where(candidate => candidate.Id == newImageId.Value && candidate.HostAccountId == hostAccountId)
                    .SingleOrDefaultAsync(cancellationToken);

                if (newImage is null || await _dbContext.Questions
                        .AnyAsync(existing => existing.ImageId == newImage.Id, cancellationToken))
                {
                    return Result.Failure<QuestionResponse>(QuizErrors.InvalidImageReference);
                }

                imageUrl = newImage.StoragePath;
                newImage.UnreferencedSince = null;
            }

            if (oldImageId.HasValue)
            {
                QuestionImage? oldImage = await _dbContext.QuestionImages
                    .TagWith("Quizzes:UpdateQuestion:GetOldImage")
                    .Where(candidate => candidate.Id == oldImageId.Value && candidate.HostAccountId == hostAccountId)
                    .SingleOrDefaultAsync(cancellationToken);

                if (oldImage is not null &&
                    !await _dbContext.GameQuestionSnapshots
                        .AnyAsync(snapshot => snapshot.ImageId == oldImage.Id, cancellationToken))
                {
                    oldImage.UnreferencedSince = _timeProvider.GetUtcNow();
                }
            }
        }

        if (oldImageId == newImageId && newImageId.HasValue)
        {
            imageUrl = await _dbContext.QuestionImages
                .AsNoTracking()
                .Where(image => image.Id == newImageId.Value && image.HostAccountId == hostAccountId)
                .Select(image => image.StoragePath)
                .SingleOrDefaultAsync(cancellationToken);
        }

        await _dbContext.Choices
            .TagWith("Quizzes:UpdateQuestion:DeleteOldChoices")
            .Where(c => c.QuestionId == question.Id && c.HostAccountId == hostAccountId)
            .ExecuteDeleteAsync(cancellationToken);

        List<Choice> newChoices = new List<Choice>(request.Choices.Count);
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

            newChoices.Add(choice);
            choiceResponses.Add(new ChoiceResponse(
                choice.Id,
                choice.OrderIndex,
                choice.Text,
                choice.IsCorrect));
        }

        _dbContext.Choices.AddRange(newChoices);

        question.Text = request.Text.Trim();
        question.ImageId = request.ImageId;
        question.DurationSeconds = request.DurationSeconds;
        question.BasePoints = request.BasePoints;

        quiz.Revision++;

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
