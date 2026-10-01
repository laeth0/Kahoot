using FluentValidation.Results;
using Kahoot.Application.Features.Auth.ChangePassword;
using Xunit;

namespace Kahoot.Application.UnitTests.Features.Auth;

public sealed class ChangePasswordCommandValidatorTests
{
    private readonly ChangePasswordCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidRequest_Succeeds()
    {
        ChangePasswordCommand command = new("OldP@ssword123!", "NewP@ssword123!");

        ValidationResult result = _validator.Validate(command);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_WhenCurrentPasswordIsEmpty_FailsWithRequiredMessage(string? currentPassword)
    {
        ChangePasswordCommand command = new(currentPassword!, "NewP@ssword123!");

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Current password is required.");
    }

    [Fact]
    public void Validate_WhenCurrentPasswordExceeds128Characters_FailsWithTooLongMessage()
    {
        string longPassword = new('a', 129);
        ChangePasswordCommand command = new(longPassword, "NewP@ssword123!");

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Current password is too long.");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_WhenNewPasswordIsEmpty_FailsWithRequiredMessage(string? newPassword)
    {
        ChangePasswordCommand command = new("OldP@ssword123!", newPassword!);

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "New password is required.");
    }

    [Fact]
    public void Validate_WhenNewPasswordShorterThan12Characters_FailsWithLengthMessage()
    {
        ChangePasswordCommand command = new("OldP@ssword123!", "Short1!Aa");

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("Password must be between 12 and 128 characters."));
    }

    [Fact]
    public void Validate_WhenNewPasswordExceeds128Characters_FailsWithLengthMessage()
    {
        string longPassword = "A1!" + new string('a', 126);
        ChangePasswordCommand command = new("OldP@ssword123!", longPassword);

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("Password must be between 12 and 128 characters."));
    }

    [Fact]
    public void Validate_WhenNewPasswordLacksUppercase_FailsWithUppercaseMessage()
    {
        ChangePasswordCommand command = new("OldP@ssword123!", "lowercase123!@#");

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Password must contain at least one uppercase letter.");
    }

    [Fact]
    public void Validate_WhenNewPasswordLacksLowercase_FailsWithLowercaseMessage()
    {
        ChangePasswordCommand command = new("OldP@ssword123!", "UPPERCASE123!@#");

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Password must contain at least one lowercase letter.");
    }

    [Fact]
    public void Validate_WhenNewPasswordLacksDigit_FailsWithDigitMessage()
    {
        ChangePasswordCommand command = new("OldP@ssword123!", "NoDigitsHereP@ssword");

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Password must contain at least one digit.");
    }

    [Fact]
    public void Validate_WhenNewPasswordLacksSpecialCharacter_FailsWithSpecialCharacterMessage()
    {
        ChangePasswordCommand command = new("OldP@ssword123!", "NoSpecialChar12345");

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Password must contain at least one special character.");
    }
}
