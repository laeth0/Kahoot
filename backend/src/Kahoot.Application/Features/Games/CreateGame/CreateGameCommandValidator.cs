namespace Kahoot.Application.Features.Games.CreateGame;

using FluentValidation;

public sealed class CreateGameCommandValidator : AbstractValidator<CreateGameCommand>
{
    public CreateGameCommandValidator()
    {
        // Fail-Fast Boundary Validation - Ensures target Quiz identifier is non-empty before initiating game session creation
        RuleFor(command => command.QuizId)
            .NotEmpty()
            .WithMessage("QuizId is required.");
    }
}
