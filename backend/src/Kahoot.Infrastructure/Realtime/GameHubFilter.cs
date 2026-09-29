namespace Kahoot.Infrastructure.Realtime;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

// Realtime Hub Envelope Exception Filter (RT-HUB-001) - Normalizes uncaught hub exceptions into standard typed response envelopes, guaranteeing client protocol stability without leaking internal stack traces.
public sealed class GameHubFilter : IHubFilter
{
    private readonly ILogger<GameHubFilter> _logger;

    public GameHubFilter(ILogger<GameHubFilter> logger)
    {
        _logger = logger;
    }

    // Hub Invocation Interceptor - Intercepts all hub method executions to enforce uniform envelope response contracts.
    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        try
        {
            return await next(invocationContext);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Unhandled hub method exception. Method={Method} ConnectionId={ConnectionId}",
                invocationContext.HubMethodName,
                invocationContext.Context.ConnectionId);

            // RFC 7807 Error Envelope Normalization - Prevents raw SignalR InvocationCompletion errors by encapsulating exceptions into typed envelope failure payloads.
            return new
            {
                success = false,
                data = (object?)null,
                error = new
                {
                    code = "Server.InternalError",
                    description = "An unexpected server error occurred."
                }
            };
        }
    }
}
