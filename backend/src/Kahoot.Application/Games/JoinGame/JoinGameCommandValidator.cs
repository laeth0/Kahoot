using FluentValidation;

namespace Kahoot.Application.Games.JoinGame;

public sealed class JoinGameCommandValidator : AbstractValidator<JoinGameCommand>
{
    public JoinGameCommandValidator()
    {
        RuleFor(command => command.Pin)
            .NotEmpty()
            .Matches("^[0-9]{4,8}$")
            .WithMessage("The game PIN must be 4 to 8 digits.");

        RuleFor(command => command.Nickname)
            .NotEmpty()
            .MinimumLength(2)
            .MaximumLength(30)
            .Matches(@"^[\p{L}\p{N} _.\-]+$")
            .WithMessage("The nickname may only contain letters, digits, spaces, '_', '.' and '-'.");
    }
}
