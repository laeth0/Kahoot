using FluentValidation;

namespace Kahoot.Application.Authentication.Register;

public sealed class RegisterHostCommandValidator : AbstractValidator<RegisterHostCommand>
{
    public RegisterHostCommandValidator()
    {
        RuleFor(command => command.Username)
            .NotEmpty()
            .MinimumLength(3)
            .MaximumLength(64)
            .Matches("^[A-Za-z0-9._-]+$")
            .WithMessage("Username may only contain letters, digits, '.', '_' and '-'.");

        RuleFor(command => command.Password)
            .NotEmpty()
            .MinimumLength(8)
            .MaximumLength(128);
    }
}
