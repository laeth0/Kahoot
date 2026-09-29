using FluentValidation;

namespace Kahoot.Application.Features.Auth.Login;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        // Boundary Validation - Rejects empty inputs and bounds raw payload size before normalization
        RuleFor(command => command.Username)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Username is required.")
            .MaximumLength(256).WithMessage("Username is too long.");

        // Boundary Validation - Bounds raw password input size before verification
        RuleFor(command => command.Password)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Password is required.")
            .MaximumLength(128).WithMessage("Password is too long.");
    }
}
