using FluentValidation;
using Kahoot.Application.Features.Quizzes.Questions;

namespace Kahoot.Application.Features.Quizzes.Questions.AddQuestion;

public sealed class AddQuestionCommandValidator : AbstractValidator<AddQuestionCommand>
{
    private const int MinTextLength = 1;
    private const int MaxTextLength = 500;
    private const int MinDurationSeconds = 5;
    private const int MaxDurationSeconds = 300;
    private const int MinBasePoints = 0;
    private const int MaxBasePoints = int.MaxValue;
    private const int MinChoicesCount = 2;
    private const int MaxChoicesCount = 6;

    public AddQuestionCommandValidator()
    {
        // Target Resource Validation - Ensures non-empty identifier for parent quiz
        RuleFor(command => command.QuizId)
            .NotEmpty()
            .WithMessage("QuizId is required.");

        // Question Text Boundary Validation (QUIZ-QUEST-001) - Enforces non-empty plain-text between 1 and 500 characters after trimming
        RuleFor(command => command.Text)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Question text is required.")
            .Must(text => !string.IsNullOrWhiteSpace(text))
            .WithMessage("Question text is required.")
            .Must(text => text.Trim().Length is >= MinTextLength and <= MaxTextLength)
            .WithMessage($"Question text must be between {MinTextLength} and {MaxTextLength} characters.");

        // Duration Boundary Validation (QUIZ-QUEST-002) - Constrains question countdown timer between 5 and 300 seconds
        RuleFor(command => command.DurationSeconds)
            .InclusiveBetween(MinDurationSeconds, MaxDurationSeconds)
            .WithMessage($"DurationSeconds must be between {MinDurationSeconds} and {MaxDurationSeconds} seconds.");

        // Base Points Boundary Validation (QUIZ-QUEST-003) - Enforces non-negative base score weighting (0 to int.MaxValue)
        RuleFor(command => command.BasePoints)
            .InclusiveBetween(MinBasePoints, MaxBasePoints)
            .WithMessage($"BasePoints must be between {MinBasePoints} and {MaxBasePoints}.");

        // Choice Cardinality & Correctness Invariants (QUIZ-QUEST-004, QUIZ-ERR-005) - Enforces 2 to 6 choices and at least one correct choice
        RuleFor(command => command.Choices)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .WithMessage("Choices are required.")
            .Must(choices => choices.Count is >= MinChoicesCount and <= MaxChoicesCount)
            .WithMessage($"A question must contain between {MinChoicesCount} and {MaxChoicesCount} choices.")
            .Must(choices => choices.Any(choice => choice.IsCorrect))
            .WithMessage("At least one choice must be marked as correct.");

        // Nested Collection Item Validation - Validates each individual choice against payload constraints
        RuleForEach(command => command.Choices)
            .SetValidator(new ChoiceRequestValidator());
    }
}

public sealed class ChoiceRequestValidator : AbstractValidator<ChoiceRequest>
{
    private const int MinChoiceTextLength = 1;
    private const int MaxChoiceTextLength = 300;

    public ChoiceRequestValidator()
    {
        // Choice Text Boundary Validation (QUIZ-QUEST-004) - Enforces non-empty trimmed choice text between 1 and 300 characters
        RuleFor(choice => choice.Text)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Choice text is required.")
            .Must(text => !string.IsNullOrWhiteSpace(text))
            .WithMessage("Choice text is required.")
            .Must(text => text.Trim().Length is >= MinChoiceTextLength and <= MaxChoiceTextLength)
            .WithMessage($"Choice text must be between {MinChoiceTextLength} and {MaxChoiceTextLength} characters.");
    }
}
