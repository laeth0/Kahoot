using FluentValidation;

namespace Kahoot.Application.Features.Quizzes.GetQuizById;

public sealed class GetQuizByIdQueryValidator : AbstractValidator<GetQuizByIdQuery>
{
    public GetQuizByIdQueryValidator()
    {
        // Target Resource Validation - Ensures target quiz identifier is non-empty before query execution
        RuleFor(query => query.QuizId)
            .NotEmpty()
            .WithMessage("QuizId is required.");
    }
}
