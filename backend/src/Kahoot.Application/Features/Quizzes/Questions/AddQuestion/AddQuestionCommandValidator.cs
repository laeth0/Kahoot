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
        RuleFor(command => command.QuizId)
            .NotEmpty()
            .WithMessage("QuizId is required.");

        RuleFor(command => command.Text)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Question text is required.")
            .Must(text => !string.IsNullOrWhiteSpace(text))
            .WithMessage("Question text is required.")
            .Must(text => text.Trim().Length is >= MinTextLength and <= MaxTextLength)
            .WithMessage($"Question text must be between {MinTextLength} and {MaxTextLength} characters.");

        RuleFor(command => command.DurationSeconds)
            .InclusiveBetween(MinDurationSeconds, MaxDurationSeconds)
            .WithMessage($"DurationSeconds must be between {MinDurationSeconds} and {MaxDurationSeconds} seconds.");

        RuleFor(command => command.BasePoints)
            .InclusiveBetween(MinBasePoints, MaxBasePoints)
            .WithMessage($"BasePoints must be between {MinBasePoints} and {MaxBasePoints}.");

        RuleFor(command => command.Choices)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .WithMessage("Choices are required.")
            .Must(choices => choices.Count is >= MinChoicesCount and <= MaxChoicesCount)
            .WithMessage($"A question must contain between {MinChoicesCount} and {MaxChoicesCount} choices.")
            .Must(choices => choices.Any(choice => choice.IsCorrect))
            .WithMessage("At least one choice must be marked as correct.");

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
