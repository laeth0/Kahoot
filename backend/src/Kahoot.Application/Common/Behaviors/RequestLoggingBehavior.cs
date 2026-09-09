using Kahoot.Domain.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kahoot.Application.Common.Behaviors;

public sealed class RequestLoggingBehavior<TRequest, TResponse>(
    ILogger<RequestLoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
    where TResponse : Result
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        string requestName = typeof(TRequest).Name;

        TResponse response = await next(cancellationToken);

        if (response.IsFailure)
        {
            logger.LogWarning(
                "{RequestName} completed with failure {ErrorCode}: {ErrorDescription}",
                requestName,
                response.Error.Code,
                response.Error.Description);
        }

        return response;
    }
}
