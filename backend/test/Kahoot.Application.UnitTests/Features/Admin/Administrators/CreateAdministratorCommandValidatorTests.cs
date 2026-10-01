using FluentValidation.Results;
using Kahoot.Application.Features.Admin.Administrators.CreateAdministrator;
using Xunit;

namespace Kahoot.Application.UnitTests.Features.Admin.Administrators;

public sealed class CreateAdministratorCommandValidatorTests
{
    private readonly CreateAdministratorCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidRequest_Succeeds()
    {
        CreateAdministratorCommand command = new("admin_user-1", "ValidP@ssword123!");

        ValidationResult result = _validator.Validate(command);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_WhenUsernameIsEmpty_FailsWithRequiredMessage(string? username)
    {
        CreateAdministratorCommand command = new(username!, "ValidP@ssword123!");

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Username is required.");
    }

    [Theory]
    [InlineData("ab")] // 2 chars
    [InlineData("a")] // 1 char
    public void Validate_WhenUsernameTooShort_FailsWithLengthMessage(string shortUsername)
    {
        CreateAdministratorCommand command = new(shortUsername, "ValidP@ssword123!");

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("Username must be between 3 and 64 characters."));
    }

    [Fact]
    public void Validate_WhenUsernameTooLong_FailsWithLengthMessage()
    {
        string longUsername = new('a', 65);
        CreateAdministratorCommand command = new(longUsername, "ValidP@ssword123!");

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("Username must be between 3 and 64 characters."));
    }

    [Theory]
    [InlineData("admin user")] // space
    [InlineData("admin@user")] // @
    [InlineData("admin!user")] // !
    [InlineData("admin.user")] // dot
    public void Validate_WhenUsernameContainsDisallowedCharacters_FailsWithAllowedCharactersMessage(string disallowedUsername)
    {
        CreateAdministratorCommand command = new(disallowedUsername, "ValidP@ssword123!");

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("Username must contain only alphanumeric characters, hyphens, and underscores."));
    }

    [Theory]
    [InlineData("admin\u0000user")] // control char
    [InlineData("admin\u200Buser")] // zero-width format char
    [InlineData("admin\uD83D\uDE00")] // surrogate char
    public void Validate_WhenUsernameContainsProhibitedCharacters_FailsWithInvalidCharactersMessage(string invalidUsername)
    {
        CreateAdministratorCommand command = new(invalidUsername, "ValidP@ssword123!");

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("Username contains invalid characters"));
    }

    [Fact]
    public void Validate_WhenPasswordTooShort_FailsWithLengthMessage()
    {
        CreateAdministratorCommand command = new("admin_1", "Short1!Aa");

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("Password must be between 12 and 128 characters."));
    }

    [Fact]
    public void Validate_WhenPasswordLacksUppercase_Fails()
    {
        CreateAdministratorCommand command = new("admin_1", "lowercase123!@#");

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Password must contain at least one uppercase letter.");
    }

    [Fact]
    public void Validate_WhenPasswordLacksLowercase_Fails()
    {
        CreateAdministratorCommand command = new("admin_1", "UPPERCASE123!@#");

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Password must contain at least one lowercase letter.");
    }

    [Fact]
    public void Validate_WhenPasswordLacksDigit_Fails()
    {
        CreateAdministratorCommand command = new("admin_1", "NoDigitsHereP@ssword");

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Password must contain at least one digit.");
    }

    [Fact]
    public void Validate_WhenPasswordLacksSpecialCharacter_Fails()
    {
        CreateAdministratorCommand command = new("admin_1", "NoSpecialChar12345");

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Password must contain at least one special character.");
    }
}
