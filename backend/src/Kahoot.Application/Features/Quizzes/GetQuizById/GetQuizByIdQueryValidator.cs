using FluentValidation;

namespace Kahoot.Application.Features.Quizzes.GetQuizById;

public sealed class GetQuizByIdQueryValidator : AbstractValidator<GetQuizByIdQuery>
{
    public GetQuizByIdQueryValidator()
    {
        RuleFor(query => query.QuizId)
            .NotEmpty()
            .WithMessage("QuizId is required.");
    }
}
