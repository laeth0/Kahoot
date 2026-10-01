using FluentValidation.Results;
using Kahoot.Application.Features.Games.GetJoinInfo;
using Xunit;

namespace Kahoot.Application.UnitTests.Features.Games.GetJoinInfo;

public sealed class GetJoinInfoQueryValidatorTests
{
    private readonly GetJoinInfoQueryValidator _validator = new();

    [Theory]
    [InlineData("1234")]
    [InlineData("123456")]
    [InlineData("12345678")]
    public void Validate_WithValidPin_Succeeds(string pin)
    {
        GetJoinInfoQuery query = new(pin);

        ValidationResult result = _validator.Validate(query);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validate_WhenPinIsEmpty_FailsWithRequiredMessage(string? pin)
    {
        GetJoinInfoQuery query = new(pin!);

        ValidationResult result = _validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "PIN is required.");
    }

    [Theory]
    [InlineData("123")]
    [InlineData("123456789")]
    [InlineData("abcd")]
    public void Validate_WhenPinIsInvalid_FailsWithNumericDigitsMessage(string pin)
    {
        GetJoinInfoQuery query = new(pin);

        ValidationResult result = _validator.Validate(query);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "PIN must be 4 to 8 numeric digits.");
    }
}
