using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Kahoot.Api.Common;

public sealed class RequestCorrelationMiddleware(RequestDelegate next, ILogger<RequestCorrelationMiddleware> logger)
{
    private const string HeaderName = "X-Request-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        string requestId = context.Request.Headers.TryGetValue(HeaderName, out var headerValues)
            && IsValidRequestId(headerValues.ToString())
            ? headerValues.ToString()
            : ActivityTraceId.CreateRandom().ToString();

        context.Response.Headers[HeaderName] = requestId;
        Activity.Current?.SetTag("request.id", requestId);

        using (logger.BeginScope(new Dictionary<string, object> { ["request.id"] = requestId }))
        {
            await next(context);
        }
    }

    private static bool IsValidRequestId(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > 64)
        {
            return false;
        }

        foreach (char c in value)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c != '.' && c != '_' && c != '-')
            {
                return false;
            }
        }

        return true;
    }
}
