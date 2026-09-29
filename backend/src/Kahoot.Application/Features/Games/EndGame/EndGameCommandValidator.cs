namespace Kahoot.Application.Features.Games.EndGame;

using FluentValidation;

public sealed class EndGameCommandValidator : AbstractValidator<EndGameCommand>
{
    public EndGameCommandValidator()
    {
        RuleFor(command => command.GameId)
            .NotEmpty()
            .WithMessage("GameId is required.");

        RuleFor(command => command.CommandId)
            .NotEmpty()
            .WithMessage("CommandId is required.");

        RuleFor(command => command.ExpectedStateVersion)
            .GreaterThanOrEqualTo(1)
            .WithMessage("ExpectedStateVersion must be greater than or equal to 1.");
    }
}
