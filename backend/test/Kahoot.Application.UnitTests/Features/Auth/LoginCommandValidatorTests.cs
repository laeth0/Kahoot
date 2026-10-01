using FluentValidation.Results;
using Kahoot.Application.Features.Auth.Login;
using Xunit;

namespace Kahoot.Application.UnitTests.Features.Auth;

public sealed class LoginCommandValidatorTests
{
    private readonly LoginCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidRequest_Succeeds()
    {
        LoginCommand command = new("user", "pass", "127.0.0.1");

        ValidationResult result = _validator.Validate(command);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_WithSimplePassword_SucceedsWithoutEnforcingRegistrationComplexity()
    {
        // Login does not enforce registration complexity (12 chars, mixed case, symbols)
        LoginCommand command = new("valid_user", "simple", "127.0.0.1");

        ValidationResult result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Validate_WhenUsernameIsEmpty_FailsWithRequiredMessage(string? username)
    {
        LoginCommand command = new(username!, "password", "127.0.0.1");

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Username is required.");
    }

    [Fact]
    public void Validate_WhenUsernameExceeds256Characters_FailsWithTooLongMessage()
    {
        string longUsername = new('a', 257);
        LoginCommand command = new(longUsername, "password", "127.0.0.1");

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Username is too long.");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_WhenPasswordIsEmpty_FailsWithRequiredMessage(string? password)
    {
        LoginCommand command = new("user", password!, "127.0.0.1");

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Password is required.");
    }

    [Fact]
    public void Validate_WhenPasswordExceeds128Characters_FailsWithTooLongMessage()
    {
        string longPassword = new('a', 129);
        LoginCommand command = new("user", longPassword, "127.0.0.1");

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Password is too long.");
    }
}
