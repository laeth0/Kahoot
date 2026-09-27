using FluentValidation;

namespace Kahoot.Application.Features.Quizzes.PublishQuiz;

public sealed class PublishQuizCommandValidator : AbstractValidator<PublishQuizCommand>
{
    public PublishQuizCommandValidator()
    {
        RuleFor(command => command.QuizId)
            .NotEmpty()
            .WithMessage("QuizId is required.");
    }
}
