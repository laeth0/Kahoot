namespace Kahoot.Application.Features.Games.JoinGame;

using System.Text.RegularExpressions;
using FluentValidation;

public sealed class JoinGameCommandValidator : AbstractValidator<JoinGameCommand>
{
    private static readonly Regex PinRegex = new(@"^[0-9]{4,8}$", RegexOptions.Compiled);

    public JoinGameCommandValidator()
    {
        RuleFor(command => command.Pin)
            .NotEmpty()
            .WithMessage("PIN is required.")
            .Must(pin => !string.IsNullOrWhiteSpace(pin) && PinRegex.IsMatch(pin))
            .WithMessage("PIN must be 4 to 8 numeric digits.");

        RuleFor(command => command.Nickname)
            .NotEmpty()
            .WithMessage("Nickname is required.")
            .Must(nickname => PlayerNickname.TryNormalize(nickname, out _, out _))
            .WithMessage("Nickname must be 2 to 30 printable characters without control or format characters.");

        RuleFor(command => command.JoinOperationId)
            .Must(operationId => operationId != Guid.Empty && operationId.Version == 4)
            .WithMessage("JoinOperationId must be a valid non-empty UUIDv4.");
    }
}
