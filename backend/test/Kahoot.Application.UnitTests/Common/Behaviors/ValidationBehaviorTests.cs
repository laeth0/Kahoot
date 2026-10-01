using FluentValidation;
using FluentValidation.Results;
using Kahoot.Application.Common.Behaviors;
using MediatR;
using Xunit;

namespace Kahoot.Application.UnitTests.Common.Behaviors;

public sealed class ValidationBehaviorTests
{
    private sealed record TestRequest(string Name);
    private sealed record TestResponse(string Status);

    [Fact]
    public async Task Handle_WhenNoValidatorsRegistered_CallsNextDelegateExactlyOnce()
    {
        IValidator<TestRequest>[] validators = Array.Empty<IValidator<TestRequest>>();
        ValidationBehavior<TestRequest, TestResponse> behavior = new(validators);
        TestRequest request = new("Sample");
        int nextCallCount = 0;

        TestResponse response = await behavior.Handle(
            request,
            (cancellationToken) =>
            {
                nextCallCount++;
                return Task.FromResult(new TestResponse("Success"));
            },
            CancellationToken.None);

        Assert.Equal(1, nextCallCount);
        Assert.Equal("Success", response.Status);
    }

    [Fact]
    public async Task Handle_WhenRequestIsValid_CallsNextDelegateExactlyOnce()
    {
        InlineValidator<TestRequest> validator = new();
        validator.RuleFor(x => x.Name).NotEmpty();

        ValidationBehavior<TestRequest, TestResponse> behavior = new(new[] { validator });
        TestRequest request = new("ValidName");
        int nextCallCount = 0;

        TestResponse response = await behavior.Handle(
            request,
            (cancellationToken) =>
            {
                nextCallCount++;
                return Task.FromResult(new TestResponse("Success"));
            },
            CancellationToken.None);

        Assert.Equal(1, nextCallCount);
        Assert.Equal("Success", response.Status);
    }

    [Fact]
    public async Task Handle_WhenRequestIsInvalid_ThrowsValidationExceptionAndPreventsNextDelegateExecution()
    {
        InlineValidator<TestRequest> validator = new();
        validator.RuleFor(x => x.Name).NotEmpty().WithMessage("Name cannot be empty.");

        ValidationBehavior<TestRequest, TestResponse> behavior = new(new[] { validator });
        TestRequest request = new(string.Empty);
        int nextCallCount = 0;

        ValidationException exception = await Assert.ThrowsAsync<ValidationException>(
            () => behavior.Handle(
                request,
                (cancellationToken) =>
                {
                    nextCallCount++;
                    return Task.FromResult(new TestResponse("ShouldNotExecute"));
                },
                CancellationToken.None));

        Assert.Equal(0, nextCallCount);
        Assert.Single(exception.Errors);
        Assert.Equal("Name cannot be empty.", exception.Errors.First().ErrorMessage);
    }

    [Fact]
    public async Task Handle_WhenMultipleValidatorsProduceErrors_AggregatesAllFailures()
    {
        InlineValidator<TestRequest> validator1 = new();
        validator1.RuleFor(x => x.Name).Must(name => name.StartsWith("PREFIX_")).WithMessage("Must start with PREFIX_");

        InlineValidator<TestRequest> validator2 = new();
        validator2.RuleFor(x => x.Name).MinimumLength(10).WithMessage("Must have length >= 10");
        validator2.RuleFor(x => x.Name).Must(name => !name.Contains(" ")).WithMessage("Must not contain whitespace");

        ValidationBehavior<TestRequest, TestResponse> behavior = new(new[] { validator1, validator2 });
        TestRequest request = new("bad input");
        int nextCallCount = 0;

        ValidationException exception = await Assert.ThrowsAsync<ValidationException>(
            () => behavior.Handle(
                request,
                (cancellationToken) =>
                {
                    nextCallCount++;
                    return Task.FromResult(new TestResponse("ShouldNotExecute"));
                },
                CancellationToken.None));

        Assert.Equal(0, nextCallCount);
        List<ValidationFailure> errorList = exception.Errors.ToList();
        Assert.NotEmpty(errorList);
        Assert.Contains(errorList, e => e.ErrorMessage == "Must start with PREFIX_");
        Assert.Contains(errorList, e => e.ErrorMessage == "Must have length >= 10");
        Assert.Contains(errorList, e => e.ErrorMessage == "Must not contain whitespace");
        Assert.Equal(3, errorList.Select(e => e.ErrorMessage).Distinct().Count());
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToValidators()
    {
        CancellationTrackingValidator validator = new();
        ValidationBehavior<TestRequest, TestResponse> behavior = new(new[] { validator });
        TestRequest request = new("Sample");

        using CancellationTokenSource cts = new();
        CancellationToken expectedToken = cts.Token;

        await behavior.Handle(
            request,
            (cancellationToken) => Task.FromResult(new TestResponse("Success")),
            expectedToken);

        Assert.Equal(expectedToken, validator.ReceivedToken);
    }

    private sealed class CancellationTrackingValidator : IValidator<TestRequest>
    {
        public CancellationToken ReceivedToken { get; private set; }

        public Task<ValidationResult> ValidateAsync(
            IValidationContext context,
            CancellationToken cancellation = default)
        {
            ReceivedToken = cancellation;
            return Task.FromResult(new ValidationResult());
        }

        public ValidationResult Validate(IValidationContext context) => new();

        public ValidationResult Validate(TestRequest instance) => new();

        public Task<ValidationResult> ValidateAsync(
            TestRequest instance,
            CancellationToken cancellation = default)
        {
            ReceivedToken = cancellation;
            return Task.FromResult(new ValidationResult());
        }

        public IValidatorDescriptor CreateDescriptor() => throw new NotImplementedException();

        public bool CanValidateInstancesOfType(Type type) => type == typeof(TestRequest);
    }
}
