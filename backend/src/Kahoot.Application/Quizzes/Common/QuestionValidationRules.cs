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
            .Must(choices => choices.Any(choice => choice.IsCorrect))
            .WithMessage("At least one choice must be marked correct.");
}

public sealed class ChoiceInputValidator : AbstractValidator<ChoiceInput>
{
    public ChoiceInputValidator()
    {
        RuleFor(choice => choice.Text).MaximumLength(QuestionValidationRules.MaxChoiceTextLength);
        RuleFor(choice => choice.ImageUrl).MaximumLength(QuestionValidationRules.MaxImageUrlLength);
        RuleFor(choice => choice)
            .Must(choice => !string.IsNullOrWhiteSpace(choice.Text) || !string.IsNullOrWhiteSpace(choice.ImageUrl))
            .WithMessage("A choice must have text or an image.");
    }
}
