using FluentValidation;

namespace Kahoot.Application.Features.Quizzes.Questions.DeleteQuestion;

public sealed class DeleteQuestionCommandValidator : AbstractValidator<DeleteQuestionCommand>
{
    public DeleteQuestionCommandValidator()
    {
        // Target Resource Validation - Ensures non-empty identifier for parent quiz
        RuleFor(command => command.QuizId)
            .NotEmpty()
            .WithMessage("QuizId is required.");

        // Target Resource Validation - Ensures non-empty identifier for target question
        RuleFor(command => command.QuestionId)
            .NotEmpty()
            .WithMessage("QuestionId is required.");
    }
}
