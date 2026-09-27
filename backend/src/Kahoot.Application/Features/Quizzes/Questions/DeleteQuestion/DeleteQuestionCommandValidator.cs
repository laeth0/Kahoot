using FluentValidation;

namespace Kahoot.Application.Features.Quizzes.Questions.DeleteQuestion;

public sealed class DeleteQuestionCommandValidator : AbstractValidator<DeleteQuestionCommand>
{
    public DeleteQuestionCommandValidator()
    {
        RuleFor(command => command.QuizId)
            .NotEmpty()
            .WithMessage("QuizId is required.");

        RuleFor(command => command.QuestionId)
            .NotEmpty()
            .WithMessage("QuestionId is required.");
    }
}
