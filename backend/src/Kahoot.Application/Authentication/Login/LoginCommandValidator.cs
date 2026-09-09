using FluentValidation;

namespace Kahoot.Application.Authentication.Login;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(command => command.Username).NotEmpty().MaximumLength(64);
        RuleFor(command => command.Password).NotEmpty().MaximumLength(128);
    }
}
