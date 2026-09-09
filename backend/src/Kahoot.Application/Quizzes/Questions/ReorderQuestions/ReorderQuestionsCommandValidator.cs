using FluentValidation;

namespace Kahoot.Application.Quizzes.Questions.ReorderQuestions;

public sealed class ReorderQuestionsCommandValidator : AbstractValidator<ReorderQuestionsCommand>
{
    public ReorderQuestionsCommandValidator()
    {
        RuleFor(command => command.QuizId).NotEmpty();
        RuleFor(command => command.OrderedQuestionIds)
            .NotNull()
            .Must(ids => ids.Count > 0).WithMessage("At least one question id is required.")
            .Must(ids => ids.Distinct().Count() == ids.Count).WithMessage("Question ids must be unique.");
    }
}
