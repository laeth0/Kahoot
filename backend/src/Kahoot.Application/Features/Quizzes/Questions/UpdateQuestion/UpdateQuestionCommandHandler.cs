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
        // Tenant Isolation - Authenticates host session before accepting question modification
        if (!_currentUser.UserId.HasValue)
        {
            return Result.Failure<QuestionResponse>(AuthErrors.Unauthorized);
        }

        Guid hostAccountId = _currentUser.UserId.Value;

        // Transactional Consistency Boundary - Serializes host quiz modifications against concurrent game snapshots (QUIZ-RISK-004)
        await using IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Serialized Host Mutation Barrier - Acquires SELECT FOR UPDATE on host row to serialize question updates
        User? host = await _dbContext.GetUserForUpdateAsync(hostAccountId, cancellationToken);
        if (host is null || host.Role != UserRole.Host || host.Status != UserStatus.Active)
        {
            return Result.Failure<QuestionResponse>(AuthErrors.Unauthorized);
        }

        // Multi-Tenant Isolation - Scopes quiz seek strictly to host tenant boundary
        Quiz? quiz = await _dbContext.Quizzes
            .TagWith("Quizzes:UpdateQuestion:FindQuiz")
            .Where(q => q.Id == request.QuizId && q.HostAccountId == hostAccountId)
            .SingleOrDefaultAsync(cancellationToken);

        if (quiz is null)
        {
            return Result.Failure<QuestionResponse>(QuizErrors.NotFound);
        }

        // Active Session Lock (QUIZ-ERR-006, QUIZ-RISK-001) - Blocks question updates when active games are referencing the quiz
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

        // Multi-Tenant Question Seek - Queries target question strictly scoped by quiz ID and host account ID
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

        // Image Attachment Lifecycle Management (IMG-ATT-001, IMG-LIFE-001) - Handles replacement, attachment, or detachment of images
        if (oldImageId != newImageId)
        {
            if (newImageId.HasValue)
            {
                // Cross-Tenant Image Isolation & Single-Owner Enforcement (QUIZ-SEC-001, IMG-ATT-001) - Validates host ownership and prevents sharing across questions
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
                // Reactivation - Clears orphan timestamp when image is referenced by an active question
                newImage.UnreferencedSince = null;
            }

            if (oldImageId.HasValue)
            {
                // Detached Image Orphan Marking - Marks replaced image as unreferenced if not captured by an existing game session snapshot
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

        // Retained Image Url Resolution - Resolves storage path for unchanged image attachment without tracking overhead
        if (oldImageId == newImageId && newImageId.HasValue)
        {
            imageUrl = await _dbContext.QuestionImages
                .AsNoTracking()
                .Where(image => image.Id == newImageId.Value && image.HostAccountId == hostAccountId)
                .Select(image => image.StoragePath)
                .SingleOrDefaultAsync(cancellationToken);
        }

        // Atomic Choice Replacement (QUIZ-QUEST-004) - Drops previous choices in bulk to eliminate orphaned choice records
        await _dbContext.Choices
            .TagWith("Quizzes:UpdateQuestion:DeleteOldChoices")
            .Where(c => c.QuestionId == question.Id && c.HostAccountId == hostAccountId)
            .ExecuteDeleteAsync(cancellationToken);

        List<Choice> newChoices = new List<Choice>(request.Choices.Count);
        List<ChoiceResponse> choiceResponses = new List<ChoiceResponse>(request.Choices.Count);

        // Choice Normalization & Zero-Based Re-indexing - Sanitizes plain-text choice inputs and assigns contiguous OrderIndex
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

        // Choice Bulk Staging - Attaches replacement choices to change tracker
        _dbContext.Choices.AddRange(newChoices);

        // Question Mutation & Plain-Text Sanitization - Trims question text and mutates configured boundaries
        question.Text = request.Text.Trim();
        question.ImageId = request.ImageId;
        question.DurationSeconds = request.DurationSeconds;
        question.BasePoints = request.BasePoints;

        // Monotonic Revision Increment - Invalidates cached quiz representations and advances optimistic concurrency revision
        quiz.Revision++;

        try
        {
            // Intermediate Persistence - Flushes choice replacement and question updates to relational store
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException exception) when (
            string.Equals(exception.ConstraintName, ImageOwnershipConstraintName, StringComparison.Ordinal))
        {
            // Constraint Race Defense - Handles concurrent image association race via unique index constraint
            return Result.Failure<QuestionResponse>(QuizErrors.InvalidImageReference);
        }
        catch (ForeignKeyConstraintViolationException exception) when (
            string.Equals(exception.ConstraintName, ImageReferenceConstraintName, StringComparison.Ordinal))
        {
            // Foreign Key Integrity Defense - Handles missing or concurrently purged image reference
            return Result.Failure<QuestionResponse>(QuizErrors.InvalidImageReference);
        }
        catch (DbUpdateConcurrencyException exception) when (
            exception.Entries.Any(entry => entry.Entity is QuestionImage))
        {
            // Optimistic Concurrency Defense - Traps concurrent updates on image entity
            return Result.Failure<QuestionResponse>(QuizErrors.InvalidImageReference);
        }

        // Atomic Transaction Commit - Commits question update, choice replacement, image lifecycle, and revision increment atomically
        await transaction.CommitAsync(cancellationToken);

        // Response Projection - Maps updated question state and newly generated choices into response DTO
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
