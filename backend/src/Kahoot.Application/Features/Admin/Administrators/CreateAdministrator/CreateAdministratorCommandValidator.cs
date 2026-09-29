using System.Globalization;
using FluentValidation;
using Kahoot.Application.Features.Auth;

namespace Kahoot.Application.Features.Admin.Administrators.CreateAdministrator;

public sealed class CreateAdministratorCommandValidator : AbstractValidator<CreateAdministratorCommand>
{
    private const int MinUsernameLength = 3;
    private const int MaxUsernameLength = 64;
    private const int MaxDisplayUsernameLength = 256;
    private const int MinPasswordLength = 12;
    private const int MaxPasswordLength = 128;

    public CreateAdministratorCommandValidator()
    {
        // Fail-Fast Username Boundary Validation - Enforces NFKC printable alphanumeric rules and bounds length between 3 and 64 (ACCT-ADMIN-001)
        RuleFor(command => command.Username)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Username is required.")
            .Must(username => !HasInvalidCharacters(username))
            .WithMessage("Username contains invalid characters (control, zero-width, or surrogate characters are not allowed).")
            .Must(BeValidLength)
            .WithMessage($"Username must be between {MinUsernameLength} and {MaxUsernameLength} characters.")
            .Must(BeAllowedCharacters)
            .WithMessage("Username must contain only alphanumeric characters, hyphens, and underscores.");

        // Password Entropy Enforcement - Enforces 12 to 128 characters with upper, lower, digit, and special character requirements (ACCT-ADMIN-001)
        RuleFor(command => command.Password)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Password is required.")
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

    private static bool HasInvalidCharacters(string username)
    {
        foreach (char character in username)
        {
            UnicodeCategory category = char.GetUnicodeCategory(character);
            if (category is UnicodeCategory.Control
                or UnicodeCategory.Format
                or UnicodeCategory.Surrogate)
            {
                return true;
            }
        }

        return false;
    }

    private static bool BeValidLength(string username)
    {
        string displayUsername = UsernameNormalization.GetDisplayUsername(username);
        if (string.IsNullOrEmpty(displayUsername) || displayUsername.Length > MaxDisplayUsernameLength)
        {
            return false;
        }

        string normalized = UsernameNormalization.GetNormalizedUsername(displayUsername);
        return normalized.Length is >= MinUsernameLength and <= MaxUsernameLength;
    }

    private static bool BeAllowedCharacters(string username)
    {
        string displayUsername = UsernameNormalization.GetDisplayUsername(username);
        return displayUsername.All(character => char.IsLetterOrDigit(character) || character == '-' || character == '_');
    }
}
