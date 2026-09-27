using Kahoot.Application.Common.Interfaces;
using Kahoot.Application.Common.Messaging;
using Kahoot.Application.Common.Persistence;
using Kahoot.Application.Common.Results;
using Kahoot.Application.Features.Auth;
using Kahoot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kahoot.Application.Features.Quizzes.GetQuizById;

public sealed class GetQuizByIdQueryHandler : IQueryHandler<GetQuizByIdQuery, QuizDetailsResponse>
{
    private readonly IAppDbContext _dbContext;
    private readonly ICurrentUser _currentUser;

    public GetQuizByIdQueryHandler(
        IAppDbContext dbContext,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task<Result<QuizDetailsResponse>> Handle(
        GetQuizByIdQuery query,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            return Result.Failure<QuizDetailsResponse>(AuthErrors.Unauthorized);
        }

        Guid hostAccountId = _currentUser.UserId.Value;

        Quiz? quiz = await _dbContext.Quizzes
            .AsNoTracking()
            .TagWith("Quizzes:GetQuizById")
            .Where(q => q.Id == query.QuizId && q.HostAccountId == hostAccountId)
            .SingleOrDefaultAsync(cancellationToken);

        if (quiz is null)
        {
            return Result.Failure<QuizDetailsResponse>(QuizErrors.NotFound);
        }

        List<Question> questions = await _dbContext.Questions
            .AsNoTracking()
            .TagWith("Quizzes:GetQuizQuestions")
            .Where(question => question.QuizId == quiz.Id && question.HostAccountId == hostAccountId)
            .OrderBy(question => question.OrderIndex)
            .ToListAsync(cancellationToken);

        List<QuizQuestionDetailsResponse> questionResponses = [];

        if (questions.Count > 0)
        {
            List<Guid> questionIds = questions.Select(question => question.Id).ToList();

            List<Choice> choices = await _dbContext.Choices
                .AsNoTracking()
                .TagWith("Quizzes:GetQuizChoices")
                .Where(choice => questionIds.Contains(choice.QuestionId) && choice.HostAccountId == hostAccountId)
                .OrderBy(choice => choice.OrderIndex)
                .ToListAsync(cancellationToken);

            ILookup<Guid, Choice> choicesByQuestionId = choices.ToLookup(choice => choice.QuestionId);

            foreach (Question question in questions)
            {
                List<QuizChoiceDetailsResponse> choiceResponses = choicesByQuestionId[question.Id]
                    .Select(choice => new QuizChoiceDetailsResponse(
                        choice.Id,
                        choice.OrderIndex,
                        choice.Text,
                        choice.IsCorrect))
                    .ToList();

                questionResponses.Add(new QuizQuestionDetailsResponse(
                    question.Id,
                    question.OrderIndex,
                    question.Text,
                    question.MediaItemId,
                    question.DurationSeconds,
                    question.BasePoints,
                    choiceResponses));
            }
        }

        QuizDetailsResponse response = new QuizDetailsResponse(
            quiz.Id,
            quiz.Title,
            quiz.Description,
            quiz.IsPublished,
            quiz.Revision,
            quiz.CreatedAt,
            quiz.UpdatedAt,
            questionResponses);

        return Result.Success(response);
    }
}
