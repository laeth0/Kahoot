using System.Diagnostics;
using Kahoot.Application.Common.Observability;
using Kahoot.Domain.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Kahoot.Application.Common.Behaviors;

public sealed class RequestLoggingBehavior<TRequest, TResponse>(
    ILogger<RequestLoggingBehavior<TRequest, TResponse>> logger,
    IKahootTelemetry telemetry)
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
        using var activity = telemetry.StartOperation($"application.{requestName}");
        activity?.SetTag("kahoot.request.name", requestName);

        long startTimestamp = Stopwatch.GetTimestamp();
        string outcome = "success";

        try
        {
            TResponse response = await next(cancellationToken);

            if (response.IsFailure)
            {
                outcome = "failure";
                activity?.SetStatus(ActivityStatusCode.Error, response.Error.Description);
                activity?.SetTag("kahoot.result", "failure");
                activity?.SetTag("error.code", response.Error.Code);

                logger.LogWarning(
                    "{RequestName} completed with failure {ErrorCode}: {ErrorDescription}",
                    requestName,
                    response.Error.Code,
                    response.Error.Description);
            }
            else
            {
                activity?.SetTag("kahoot.result", "success");
                activity?.SetStatus(ActivityStatusCode.Ok);
            }

            return response;
        }
        catch (Exception ex)
        {
            outcome = "exception";
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag("kahoot.result", "exception");
            activity?.SetTag("error.type", ex.GetType().FullName);
            throw;
        }
        finally
        {
            double durationSeconds = Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds;
            telemetry.RecordOperation(requestName, outcome, durationSeconds);
        }
    }
}
