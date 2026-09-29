using FluentValidation;
using FluentValidation.Results;
using MediatR;

namespace Kahoot.Application.Common.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Pipeline Short-Circuit - Proceeds directly to next handler if no validators are registered for request
        if (!_validators.Any())
        {
            return await next();
        }

        // Validation Context Initialization - Wraps incoming request into typed FluentValidation context
        ValidationContext<TRequest> context = new ValidationContext<TRequest>(request);

        // Parallel Validator Execution - Runs all registered validators asynchronously in parallel
        ValidationResult[] validationResults = await Task.WhenAll(
            _validators.Select(validator => validator.ValidateAsync(context, cancellationToken)));

        // Failure Aggregation - Collects and flattens all validation failures across executed validators
        List<ValidationFailure> failures = validationResults
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .ToList();

        // Fail-Fast Exception Barrier - Throws ValidationException with aggregated errors to trigger global 400 Bad Request
        if (failures.Count != 0)
        {
            throw new ValidationException(failures);
        }

        return await next();
    }
}
