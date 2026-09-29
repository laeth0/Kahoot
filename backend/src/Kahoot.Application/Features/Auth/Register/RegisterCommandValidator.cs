using System.Globalization;
using FluentValidation;

namespace Kahoot.Application.Features.Auth.Register;

public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    private const int MinUsernameLength = 3;
    private const int MaxUsernameLength = 64;
    private const int MaxDisplayUsernameLength = 256;
    private const int MinPasswordLength = 12;
    private const int MaxPasswordLength = 128;

    public RegisterCommandValidator()
    {
        // Invisible Character & Spoofing Defense (AUTH-CRED-001) - Rejects control codes and zero-width spoofing
        RuleFor(command => command.Username)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Username is required.")
            .Must(username => !HasInvalidCharacters(username))
            .WithMessage("Username contains invalid characters (control, zero-width, or surrogate characters are not allowed).")
            .Must(BeValidLength)
            .WithMessage($"Username must be between {MinUsernameLength} and {MaxUsernameLength} characters.");

        // Password Entropy Contract (AUTH-CRED-002) - Enforces 12-128 chars and mixed character classes without mutating entropy
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

    // Unicode Category Sanitization - Blocks Control (Cc), Format/Zero-Width (Cf), and Surrogate (Cs)
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

    // Canonical Length Bounds - Evaluates 3-64 length after Unicode NFKC and case folding to prevent expansion exploits
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
}
