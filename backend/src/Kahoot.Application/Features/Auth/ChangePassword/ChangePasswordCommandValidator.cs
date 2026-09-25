using FluentValidation;

namespace Kahoot.Application.Features.Auth.ChangePassword;

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    private const int MinPasswordLength = 12;
    private const int MaxPasswordLength = 128;

    public ChangePasswordCommandValidator()
    {
        RuleFor(command => command.CurrentPassword)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Current password is required.");

        RuleFor(command => command.NewPassword)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("New password is required.")
            .Length(MinPasswordLength, MaxPasswordLength)
            .WithMessage($"Password must be between {MinPasswordLength} and {MaxPasswordLength} characters.")
            .Must(password => password.Any(char.IsUpper))
            .WithMessage("Password must contain at least one uppercase letter.")
            .Must(password => password.Any(char.IsLower))
            .WithMessage("Password must contain at least one lowercase letter.")
            .Must(password => password.Any(char.IsDigit))
            .WithMessage("Password must contain at least one digit.")
            .Must(password => password.Any(character => !char.IsLetterOrDigit(character)))
            .WithMessage("Password must contain at least one special character.");
    }
}
