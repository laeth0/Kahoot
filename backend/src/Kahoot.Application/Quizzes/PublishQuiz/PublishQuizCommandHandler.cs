using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Errors;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Security;
using Kahoot.Application.Quizzes.Common;
using Kahoot.Domain.Common;
using Kahoot.Domain.Quizzes;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Application.Quizzes.PublishQuiz;

internal sealed class PublishQuizCommandHandler(IApplicationDbContext dbContext, ICurrentUser currentUser)
    : ICommandHandler<PublishQuizCommand>
{
    public async Task<Result> Handle(PublishQuizCommand command, CancellationToken cancellationToken)
    {
        if (currentUser.HostId is not { } hostId)
        {
            return Result.Failure(SharedErrors.Unauthorized);
        }

        Quiz? quiz = await dbContext.Quizzes
            .Include(candidate => candidate.Questions)
            .ThenInclude(question => question.Choices)
            .FirstOrDefaultAsync(candidate => candidate.Id == command.QuizId && candidate.HostId == hostId, cancellationToken);
        if (quiz is null)
        {
            return Result.Failure(QuizErrors.NotFound);
        }

        if (!IsPublishable(quiz))
        {
            return Result.Failure(QuizErrors.NotPublishable);
        }

        quiz.IsPublished = true;
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private static bool IsPublishable(Quiz quiz)
    {
        if (quiz.Questions.Count == 0)
        {
            return false;
        }

        foreach (Question question in quiz.Questions)
        {
            int choiceCount = question.Choices.Count;
            bool validChoiceCount = choiceCount is >= QuestionValidationRules.MinChoices and <= QuestionValidationRules.MaxChoices;
            bool hasCorrectChoice = question.Choices.Any(choice => choice.IsCorrect);
            bool everyChoiceHasContent = question.Choices.All(choice =>
                !string.IsNullOrWhiteSpace(choice.Text) || !string.IsNullOrWhiteSpace(choice.ImageUrl));

            if (!validChoiceCount || !hasCorrectChoice || !everyChoiceHasContent)
            {
                return false;
            }
        }

        return true;
    }
}
