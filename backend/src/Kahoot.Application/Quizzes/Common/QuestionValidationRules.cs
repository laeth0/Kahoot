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

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    public static bool IsValidCanonicalMediaUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return true;
        }

        if (url.Length > MaxImageUrlLength)
        {
            return false;
        }

        if (!url.StartsWith("/uploads/", StringComparison.Ordinal))
        {
            return false;
        }

        string relative = url["/uploads/".Length..];
        if (string.IsNullOrWhiteSpace(relative) || relative.Contains('/') || relative.Contains('\\') || relative.Contains(".."))
        {
            return false;
        }

        int dotIndex = relative.LastIndexOf('.');
        if (dotIndex <= 0 || dotIndex == relative.Length - 1)
        {
            return false;
        }

        string rawGuid = relative[..dotIndex];
        string extension = relative[dotIndex..];

        if (!AllowedExtensions.Contains(extension))
        {
            return false;
        }

        return Guid.TryParse(rawGuid, out _);
    }

    public static IRuleBuilderOptions<T, string?> ValidCanonicalMediaUrl<T>(
        this IRuleBuilder<T, string?> ruleBuilder) =>
        ruleBuilder
            .Must(IsValidCanonicalMediaUrl)
            .WithMessage("Media URL must be a valid server-generated path in the format /uploads/<guid>.<extension> with an allowed extension (.jpg, .jpeg, .png, .webp).");

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
