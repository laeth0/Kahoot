using FluentValidation;

namespace Kahoot.Application.Features.Quizzes.CreateQuiz;

public sealed class CreateQuizCommandValidator : AbstractValidator<CreateQuizCommand>
{
    private const int MinTitleLength = 1;
    private const int MaxTitleLength = 200;
    private const int MaxDescriptionLength = 1000;

    public CreateQuizCommandValidator()
    {
        RuleFor(command => command.Title)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Title is required.")
            .Must(title => !string.IsNullOrWhiteSpace(title))
            .WithMessage("Title is required.")
            .Must(title => title.Trim().Length is >= MinTitleLength and <= MaxTitleLength)
            .WithMessage($"Title must be between {MinTitleLength} and {MaxTitleLength} characters.");

        RuleFor(command => command.Description)
            .Must(description => string.IsNullOrEmpty(description) || description.Trim().Length <= MaxDescriptionLength)
            .WithMessage($"Description cannot exceed {MaxDescriptionLength} characters.");
    }
}
