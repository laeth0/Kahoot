using Kahoot.Application.Common.Results;
using Xunit;

namespace Kahoot.Application.UnitTests.Common.Results;

public sealed class ErrorTests
{
    [Fact]
    public void None_HasEmptyPropertiesAndNoneType()
    {
        Error error = Error.None;

        Assert.Equal(string.Empty, error.Code);
        Assert.Equal(string.Empty, error.Description);
        Assert.Equal(ErrorType.None, error.Type);
    }

    [Theory]
    [InlineData("NotFound", ErrorType.NotFound)]
    [InlineData("Validation", ErrorType.Validation)]
    [InlineData("Conflict", ErrorType.Conflict)]
    [InlineData("Unauthorized", ErrorType.Unauthorized)]
    [InlineData("Forbidden", ErrorType.Forbidden)]
    [InlineData("RateLimited", ErrorType.RateLimited)]
    [InlineData("Unavailable", ErrorType.Unavailable)]
    [InlineData("Failure", ErrorType.Failure)]
    [InlineData("TooLarge", ErrorType.TooLarge)]
    [InlineData("UnsupportedType", ErrorType.UnsupportedType)]
    public void FactoryMethods_CreateExpectedErrorTypeAndPreserveCodeAndDescription(
        string factoryKind,
        ErrorType expectedType)
    {
        const string code = "Test.Code";
        const string description = "Detailed description of error.";

        Error error = factoryKind switch
        {
            "NotFound" => Error.NotFound(code, description),
            "Validation" => Error.Validation(code, description),
            "Conflict" => Error.Conflict(code, description),
            "Unauthorized" => Error.Unauthorized(code, description),
            "Forbidden" => Error.Forbidden(code, description),
            "RateLimited" => Error.RateLimited(code, description),
            "Unavailable" => Error.Unavailable(code, description),
            "Failure" => Error.Failure(code, description),
            "TooLarge" => Error.TooLarge(code, description),
            "UnsupportedType" => Error.UnsupportedType(code, description),
            _ => throw new ArgumentOutOfRangeException(nameof(factoryKind), factoryKind, "Unknown factory kind")
        };

        Assert.Equal(code, error.Code);
        Assert.Equal(description, error.Description);
        Assert.Equal(expectedType, error.Type);
    }
}
