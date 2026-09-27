using FluentValidation;

namespace Kahoot.Application.Features.Quizzes.ReorderQuestions;

public sealed class ReorderQuestionsCommandValidator : AbstractValidator<ReorderQuestionsCommand>
{
    public ReorderQuestionsCommandValidator()
    {
        RuleFor(command => command.QuizId)
            .NotEmpty()
            .WithMessage("QuizId is required.");

        RuleFor(command => command.QuestionIds)
            .NotNull()
            .WithMessage("QuestionIds are required.");
    }
}
