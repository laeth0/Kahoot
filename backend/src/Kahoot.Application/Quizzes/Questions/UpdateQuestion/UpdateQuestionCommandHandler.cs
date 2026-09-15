using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Security;
using Kahoot.Application.Common.Storage;
using Kahoot.Application.Quizzes.Common;
using Kahoot.Application.Quizzes.Questions.Common;
using Kahoot.Domain.Common;
using Kahoot.Domain.Quizzes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Kahoot.Application.Quizzes.Questions.UpdateQuestion;

internal sealed class UpdateQuestionCommandHandler(
    IApplicationDbContext dbContext,
    ICurrentUser currentUser,
    IFileStorage fileStorage) : ICommandHandler<UpdateQuestionCommand>
{
    public async Task<Result> Handle(UpdateQuestionCommand command, CancellationToken cancellationToken)
    {
        Result<Quiz> quizResult = await QuizEditGuard.LoadEditableQuizAsync(
            dbContext, currentUser, command.QuizId, cancellationToken);
        if (quizResult.IsFailure)
        {
            return Result.Failure(quizResult.Error);
        }

        Question? question = await dbContext.Questions
            .Include(candidate => candidate.Choices)
            .FirstOrDefaultAsync(
                candidate => candidate.Id == command.QuestionId && candidate.QuizId == command.QuizId,
                cancellationToken);
        if (question is null)
        {
            return Result.Failure(QuizErrors.QuestionNotFound);
        }

        string? normalizedImageUrl = QuestionMapping.Normalize(command.ImageUrl);
        if (!string.IsNullOrWhiteSpace(normalizedImageUrl) &&
            !string.Equals(question.ImageUrl, normalizedImageUrl, StringComparison.Ordinal) &&
            !fileStorage.Exists(normalizedImageUrl))
        {
            return Result.Failure(QuizErrors.InvalidMediaReference);
        }

        question.Text = command.Text.Trim();
        question.ImageUrl = normalizedImageUrl;
        question.TimeLimitSeconds = command.TimeLimitSeconds;
        question.Points = command.Points;
        quizResult.Value.IsPublished = false;

        List<Choice> existingChoices = [.. question.Choices.OrderBy(candidate => candidate.OrderIndex)];
        Dictionary<Guid, Choice> existingById = existingChoices.ToDictionary(candidate => candidate.Id);
        HashSet<Choice> remainingExisting = [.. existingChoices];
        List<Choice> updatedChoices = [];

        for (int i = 0; i < command.Choices.Count; i++)
        {
            ChoiceInput input = command.Choices[i];
            Choice? targetChoice = null;

            if (input.Id.HasValue && existingById.TryGetValue(input.Id.Value, out Choice? foundById))
            {
                targetChoice = foundById;
            }
            else if (i < existingChoices.Count && remainingExisting.Contains(existingChoices[i]))
            {
                targetChoice = existingChoices[i];
            }

            if (targetChoice is not null)
            {
                targetChoice.Text = QuestionMapping.Normalize(input.Text) ?? string.Empty;
                targetChoice.IsCorrect = input.IsCorrect;
                targetChoice.OrderIndex = i;
                remainingExisting.Remove(targetChoice);
                updatedChoices.Add(targetChoice);
            }
            else
            {
                var newChoice = new Choice
                {
                    Id = input.Id ?? Guid.CreateVersion7(),
                    QuestionId = question.Id,
                    OrderIndex = i,
                    Text = QuestionMapping.Normalize(input.Text) ?? string.Empty,
                    IsCorrect = input.IsCorrect
                };
                dbContext.Choices.Add(newChoice);
                updatedChoices.Add(newChoice);
            }
        }

        if (remainingExisting.Count > 0)
        {
            dbContext.Choices.RemoveRange(remainingExisting);
        }

        question.Choices = updatedChoices;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
