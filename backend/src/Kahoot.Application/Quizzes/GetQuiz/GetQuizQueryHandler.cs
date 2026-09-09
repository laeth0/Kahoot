using Kahoot.Application.Common.Abstractions;
using Kahoot.Application.Common.Errors;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Security;
using Kahoot.Application.Quizzes.Common;
using Kahoot.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Application.Quizzes.GetQuiz;

internal sealed class GetQuizQueryHandler(IApplicationDbContext dbContext, ICurrentUser currentUser)
    : IQueryHandler<GetQuizQuery, QuizDetailResponse>
{
    public async Task<Result<QuizDetailResponse>> Handle(GetQuizQuery query, CancellationToken cancellationToken)
    {
        if (currentUser.HostId is not { } hostId)
        {
            return Result.Failure<QuizDetailResponse>(SharedErrors.Unauthorized);
        }

        QuizDetailResponse? quiz = await dbContext.Quizzes
            .AsNoTracking()
            .Where(candidate => candidate.Id == query.QuizId && candidate.HostId == hostId)
            .Select(candidate => new QuizDetailResponse(
                candidate.Id,
                candidate.Title,
                candidate.Description,
                candidate.IsPublished,
                candidate.Questions
                    .OrderBy(question => question.OrderIndex)
                    .Select(question => new QuestionResponse(
                        question.Id,
                        question.OrderIndex,
                        question.Text,
                        question.ImageUrl,
                        question.TimeLimitSeconds,
                        question.Points,
                        question.Choices
                            .OrderBy(choice => choice.OrderIndex)
                            .Select(choice => new ChoiceResponse(
                                choice.Id,
                                choice.OrderIndex,
                                choice.Text,
                                choice.ImageUrl,
                                choice.IsCorrect))
                            .ToList()))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);

        return quiz is null
            ? Result.Failure<QuizDetailResponse>(QuizErrors.NotFound)
            : Result.Success(quiz);
    }
}
