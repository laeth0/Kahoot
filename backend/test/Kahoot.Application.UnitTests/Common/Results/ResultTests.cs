using Kahoot.Application.Common.Results;
using Xunit;

namespace Kahoot.Application.UnitTests.Common.Results;

public sealed class ResultTests
{
    [Fact]
    public void Success_CreatesSuccessfulResultWithNoneError()
    {
        Result result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void Failure_CreatesFailedResultWithSpecifiedError()
    {
        Error error = Error.Validation("Code.Test", "Validation error occurred.");

        Result result = Result.Failure(error);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void Constructor_WhenSuccessWithNonNoneError_ThrowsInvalidOperationException()
    {
        Error error = Error.Validation("Code.Test", "Error on success.");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new TestableResult(true, error));

        Assert.Equal("A successful result cannot carry an error.", exception.Message);
    }

    [Fact]
    public void Constructor_WhenFailureWithNoneError_ThrowsInvalidOperationException()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new TestableResult(false, Error.None));

        Assert.Equal("A failing result requires an error type other than None.", exception.Message);
    }

    [Fact]
    public void Constructor_WhenErrorIsNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new TestableResult(true, null!));
    }

    [Fact]
    public void GenericSuccess_WithValue_CreatesSuccessfulResultAndProvidesValue()
    {
        const string expectedValue = "payload-data";

        Result<string> result = Result.Success(expectedValue);

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(Error.None, result.Error);
        Assert.Equal(expectedValue, result.Value);
    }

    [Fact]
    public void GenericSuccess_WithNullValue_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Result.Success<string>(null!));
    }

    [Fact]
    public void GenericConstructor_WhenSuccessWithNullValue_ThrowsInvalidOperationException()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new Result<string>(null, true, Error.None));

        Assert.Equal("A successful result must provide a non-null value.", exception.Message);
    }

    [Fact]
    public void GenericFailure_CreatesFailedResultAndPreservesError()
    {
        Error error = Error.NotFound("Quiz.NotFound", "The requested quiz was not found.");

        Result<string> result = Result.Failure<string>(error);

        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Equal(error, result.Error);
    }

    [Fact]
    public void GenericFailure_AccessingValue_ThrowsInvalidOperationException()
    {
        Error error = Error.NotFound("Resource.NotFound", "Not found.");
        Result<int> result = Result.Failure<int>(error);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => _ = result.Value);

        Assert.Equal("The value of a failing result cannot be accessed.", exception.Message);
    }

    [Fact]
    public void GenericConstructor_WhenSuccessWithNonNoneError_ThrowsInvalidOperationException()
    {
        Error error = Error.Conflict("User.Conflict", "User conflict.");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new Result<string>("valid-value", true, error));

        Assert.Equal("A successful result cannot carry an error.", exception.Message);
    }

    [Fact]
    public void GenericConstructor_WhenFailureWithNoneError_ThrowsInvalidOperationException()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new Result<string>(null, false, Error.None));

        Assert.Equal("A failing result requires an error type other than None.", exception.Message);
    }

    private sealed class TestableResult : Result
    {
        public TestableResult(bool isSuccess, Error error)
            : base(isSuccess, error)
        {
        }
    }
}
