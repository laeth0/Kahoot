using FluentValidation;

namespace Kahoot.Application.Features.Quizzes.DeleteQuiz;

public sealed class DeleteQuizCommandValidator : AbstractValidator<DeleteQuizCommand>
{
    public DeleteQuizCommandValidator()
    {
        // Target Resource Validation - Ensures target quiz identifier is non-empty before processing deletion
        RuleFor(command => command.QuizId)
            .NotEmpty()
            .WithMessage("QuizId is required.");
    }
}
