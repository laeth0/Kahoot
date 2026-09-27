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

        Guid? oldMediaId = question.MediaItemId;
        Guid? newMediaId = request.MediaId;

        if (oldMediaId != newMediaId)
        {
            if (newMediaId.HasValue)
            {
                MediaItem? newMedia = await _dbContext.MediaItems
                    .TagWith("Quizzes:UpdateQuestion:ValidateNewMedia")
                    .Where(m => m.Id == newMediaId.Value && m.HostAccountId == hostAccountId)
                    .SingleOrDefaultAsync(cancellationToken);

                if (newMedia is null || newMedia.Status != MediaStatus.Active)
                {
                    return Result.Failure<QuestionResponse>(QuizErrors.InvalidMediaReference);
                }

                newMedia.ReferenceCount++;
                newMedia.UnreferencedSince = null;
            }

            if (oldMediaId.HasValue)
            {
                MediaItem? oldMedia = await _dbContext.MediaItems
                    .TagWith("Quizzes:UpdateQuestion:GetOldMedia")
                    .Where(m => m.Id == oldMediaId.Value && m.HostAccountId == hostAccountId)
                    .SingleOrDefaultAsync(cancellationToken);

                if (oldMedia is not null)
                {
                    oldMedia.ReferenceCount = Math.Max(0, oldMedia.ReferenceCount - 1);
                    if (oldMedia.ReferenceCount == 0)
                    {
                        oldMedia.UnreferencedSince = _timeProvider.GetUtcNow();
                    }
                }
            }
        }

        await using IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

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
        question.MediaItemId = request.MediaId;
        question.DurationSeconds = request.DurationSeconds;
        question.BasePoints = request.BasePoints;

        quiz.IsPublished = false;
        quiz.Revision++;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        QuestionResponse response = new QuestionResponse(
            question.Id,
            question.QuizId,
            question.OrderIndex,
            question.Text,
            question.MediaItemId,
            question.DurationSeconds,
            question.BasePoints,
            choiceResponses);

        return Result.Success(response);
    }
}
