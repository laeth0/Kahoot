using FluentValidation.Results;
using Kahoot.Application.Features.Games.JoinGame;
using Xunit;

namespace Kahoot.Application.UnitTests.Features.Games.JoinGame;

public sealed class JoinGameCommandValidatorTests
{
    private readonly JoinGameCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidRequest_Succeeds()
    {
        JoinGameCommand command = new("123456", "PlayerOne", Guid.NewGuid());

        ValidationResult result = _validator.Validate(command);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("1234")]
    [InlineData("123456")]
    [InlineData("12345678")]
    public void Validate_WhenPinIsWithinFourToEightDigits_Succeeds(string validPin)
    {
        JoinGameCommand command = new(validPin, "PlayerOne", Guid.NewGuid());

        ValidationResult result = _validator.Validate(command);

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_WhenPinIsEmpty_FailsWithRequiredMessage(string? emptyPin)
    {
        JoinGameCommand command = new(emptyPin!, "PlayerOne", Guid.NewGuid());

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "PIN is required.");
    }

    [Theory]
    [InlineData("123")] // 3 digits
    [InlineData("123456789")] // 9 digits
    [InlineData("1234a")] // Non-numeric
    [InlineData("abcdef")] // Letters
    [InlineData(" 123456 ")] // Whitespace-padded
    public void Validate_WhenPinIsNotFourToEightDigits_FailsWithNumericDigitsMessage(string invalidPin)
    {
        JoinGameCommand command = new(invalidPin, "PlayerOne", Guid.NewGuid());

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "PIN must be 4 to 8 numeric digits.");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_WhenNicknameIsEmpty_FailsWithRequiredMessage(string? emptyNickname)
    {
        JoinGameCommand command = new("123456", emptyNickname!, Guid.NewGuid());

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Nickname is required.");
    }

    [Theory]
    [InlineData("A")] // 1 character
    [InlineData("Player\u0000One")] // Control char
    public void Validate_WhenNicknameIsInvalid_FailsWithPrintableCharactersMessage(string invalidNickname)
    {
        JoinGameCommand command = new("123456", invalidNickname, Guid.NewGuid());

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage.Contains("Nickname must be 2 to 30 printable characters"));
    }

    [Fact]
    public void Validate_WhenJoinOperationIdIsEmpty_FailsWithUuidV4Message()
    {
        JoinGameCommand command = new("123456", "PlayerOne", Guid.Empty);

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "JoinOperationId must be a valid non-empty UUIDv4.");
    }

    [Fact]
    public void Validate_WhenJoinOperationIdIsNotVersion4_FailsWithUuidV4Message()
    {
        Guid version7Guid = Guid.CreateVersion7();
        JoinGameCommand command = new("123456", "PlayerOne", version7Guid);

        ValidationResult result = _validator.Validate(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "JoinOperationId must be a valid non-empty UUIDv4.");
    }
}
