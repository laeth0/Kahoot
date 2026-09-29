using FluentValidation;

namespace Kahoot.Application.Features.Quizzes.ReorderQuestions;

public sealed class ReorderQuestionsCommandValidator : AbstractValidator<ReorderQuestionsCommand>
{
    public ReorderQuestionsCommandValidator()
    {
        // Target Resource Validation - Ensures non-empty identifier for target quiz
        RuleFor(command => command.QuizId)
            .NotEmpty()
            .WithMessage("QuizId is required.");

        // Permutation Sequence Payload Validation (QUIZ-REORDER-001) - Ensures non-null question ordering collection
        RuleFor(command => command.QuestionIds)
            .NotNull()
            .WithMessage("QuestionIds are required.");
    }
}
