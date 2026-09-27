using FluentValidation;

namespace Kahoot.Application.Features.Quizzes.DeleteQuiz;

public sealed class DeleteQuizCommandValidator : AbstractValidator<DeleteQuizCommand>
{
    public DeleteQuizCommandValidator()
    {
        RuleFor(command => command.QuizId)
            .NotEmpty()
            .WithMessage("QuizId is required.");
    }
}
