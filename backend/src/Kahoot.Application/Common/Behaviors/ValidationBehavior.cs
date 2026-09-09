using FluentValidation;
using FluentValidation.Results;
using Kahoot.Application.Common.Errors;
using Kahoot.Domain.Common;
using MediatR;

namespace Kahoot.Application.Common.Behaviors;

public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : Result
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        IValidator<TRequest>[] applicable = validators as IValidator<TRequest>[] ?? validators.ToArray();
        if (applicable.Length == 0)
        {
            return await next(cancellationToken);
        }

        ValidationContext<TRequest> context = new(request);
        ValidationResult[] results = await Task.WhenAll(
            applicable.Select(validator => validator.ValidateAsync(context, cancellationToken)));

        Dictionary<string, string[]> failures = results
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .GroupBy(failure => failure.PropertyName, failure => failure.ErrorMessage)
            .ToDictionary(group => group.Key, group => group.Distinct().ToArray());

        return failures.Count == 0
            ? await next(cancellationToken)
            : BuildFailure(new ValidationError(failures));
    }

    private static TResponse BuildFailure(ValidationError error)
    {
        if (typeof(TResponse) == typeof(Result))
        {
            return (TResponse)(object)Result.Failure(error);
        }

        object failure = typeof(Result)
            .GetMethod(nameof(Result.Failure), 1, [typeof(Error)])!
            .MakeGenericMethod(typeof(TResponse).GetGenericArguments()[0])
            .Invoke(null, [error])!;

        return (TResponse)failure;
    }
}
