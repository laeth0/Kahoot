using FluentValidation.Results;
using Kahoot.Application.Features.Auth.Register;
using Xunit;

namespace Kahoot.Application.UnitTests.Features.Auth;

public sealed class RegisterCommandValidatorTests
{
    private readonly RegisterCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidRequest_Succeeds()
    {
        RegisterCommand command = new("ValidUser123", "ValidP@ssword123!");

        ValidationResult result = _validator.Validate(command);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Validate_WhenUsernameIsEmpty_FailsWithRequiredMessage(string? username)
    {
        RegisterCommand command = new(username!, "ValidP@ssword123!");

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Username is required.");
    }

    [Theory]
    [InlineData("ab")] // 2 characters
    [InlineData("a")] // 1 character
    public void Validate_WhenUsernameShorterThanMinimumLength_FailsWithLengthMessage(string shortUsername)
    {
        RegisterCommand command = new(shortUsername, "ValidP@ssword123!");

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("Username must be between 3 and 64 characters."));
    }

    [Fact]
    public void Validate_WhenUsernameExceeds64Characters_FailsWithLengthMessage()
    {
        string longUsername = new('a', 65);
        RegisterCommand command = new(longUsername, "ValidP@ssword123!");

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("Username must be between 3 and 64 characters."));
    }

    [Fact]
    public void Validate_WhenUsernameAtExactBoundaries_Succeeds()
    {
        RegisterCommand minCommand = new("abc", "ValidP@ssword123!");
        ValidationResult minResult = _validator.Validate(minCommand);
        Assert.True(minResult.IsValid);

        string max64 = new('a', 64);
        RegisterCommand maxCommand = new(max64, "ValidP@ssword123!");
        ValidationResult maxResult = _validator.Validate(maxCommand);
        Assert.True(maxResult.IsValid);
    }

    [Theory]
    [InlineData("user\u0000name")] // Control character NUL
    [InlineData("user\u0007name")] // Control character BEL
    [InlineData("user\tname")]     // Control character TAB
    [InlineData("user\r\nname")]   // Control characters CR/LF
    [InlineData("user\u200Bname")] // Format character ZERO WIDTH SPACE
    [InlineData("user\u200Ename")] // Format character LTR MARK
    [InlineData("user\uD83D\uDE00")] // Surrogate characters (Emoji)
    public void Validate_WhenUsernameContainsProhibitedCharacters_FailsWithInvalidCharactersMessage(string invalidUsername)
    {
        RegisterCommand command = new(invalidUsername, "ValidP@ssword123!");

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("Username contains invalid characters"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_WhenPasswordIsEmpty_FailsWithRequiredMessage(string? password)
    {
        RegisterCommand command = new("ValidUser", password!);

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Password is required.");
    }

    [Fact]
    public void Validate_WhenPasswordShorterThan12Characters_FailsWithLengthMessage()
    {
        RegisterCommand command = new("ValidUser", "Short1!Aa"); // 9 chars

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("Password must be between 12 and 128 characters."));
    }

    [Fact]
    public void Validate_WhenPasswordExceeds128Characters_FailsWithLengthMessage()
    {
        string longPassword = "A1!" + new string('a', 126); // 129 chars
        RegisterCommand command = new("ValidUser", longPassword);

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("Password must be between 12 and 128 characters."));
    }

    [Fact]
    public void Validate_WhenPasswordLacksUppercase_FailsWithUppercaseMessage()
    {
        RegisterCommand command = new("ValidUser", "lowercaseonly123!@#");

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Password must contain at least one uppercase letter.");
    }

    [Fact]
    public void Validate_WhenPasswordLacksLowercase_FailsWithLowercaseMessage()
    {
        RegisterCommand command = new("ValidUser", "UPPERCASEONLY123!@#");

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Password must contain at least one lowercase letter.");
    }

    [Fact]
    public void Validate_WhenPasswordLacksDigit_FailsWithDigitMessage()
    {
        RegisterCommand command = new("ValidUser", "NoDigitsHereP@ssword");

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Password must contain at least one digit.");
    }

    [Fact]
    public void Validate_WhenPasswordLacksSpecialCharacter_FailsWithSpecialCharacterMessage()
    {
        RegisterCommand command = new("ValidUser", "NoSpecialChar12345");

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Password must contain at least one special character.");
    }
}
