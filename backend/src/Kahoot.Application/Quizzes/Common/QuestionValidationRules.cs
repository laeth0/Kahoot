using FluentValidation;

namespace Kahoot.Application.Quizzes.Common;

public static class QuestionValidationRules
{
    public const int MinTimeLimitSeconds = 5;
    public const int MaxTimeLimitSeconds = 300;
    public const int MinChoices = 2;
    public const int MaxChoices = 6;
    public const int MaxQuestionTextLength = 500;
    public const int MaxChoiceTextLength = 300;
    public const int MaxImageUrlLength = 2048;

    public static IRuleBuilderOptions<T, IReadOnlyList<ChoiceInput>> ValidChoiceSet<T>(
        this IRuleBuilder<T, IReadOnlyList<ChoiceInput>> ruleBuilder) =>
        ruleBuilder
            .NotNull()
            .Must(choices => choices.Count is >= MinChoices and <= MaxChoices)
            .WithMessage($"A question must have between {MinChoices} and {MaxChoices} choices.")
            .Must(choices => choices.Count(choice => choice.IsCorrect) >= 1)
            .WithMessage("At least one choice must be marked correct.");
}

public sealed class ChoiceInputValidator : AbstractValidator<ChoiceInput>
{
    public ChoiceInputValidator()
    {
        RuleFor(choice => choice.Text)
            .NotEmpty()
            .WithMessage("A choice must have text.")
            .Must(text => !string.IsNullOrWhiteSpace(text))
            .WithMessage("A choice must have text.")
            .MaximumLength(QuestionValidationRules.MaxChoiceTextLength);
    }
}
