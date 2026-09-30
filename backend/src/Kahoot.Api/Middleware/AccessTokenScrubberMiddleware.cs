namespace Kahoot.Api.Middleware;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;

// Access Token Scrubber Middleware (OPS-LOG-002) - Sanitizes access_token query parameters to prevent credential leaks in request logs and telemetry.
internal sealed class AccessTokenScrubberMiddleware
{
    private readonly RequestDelegate _next;

    public AccessTokenScrubberMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Query Parameter Sanitization - Preserves token in HttpContext.Items for SignalR authentication while redacting from the raw query string.
        if (context.Request.Query.TryGetValue("access_token", out StringValues accessToken) &&
            !StringValues.IsNullOrEmpty(accessToken))
        {
            if (context.Request.Path.StartsWithSegments("/hubs/game", StringComparison.OrdinalIgnoreCase))
            {
                context.Items["access_token"] = accessToken.ToString();
            }

            Dictionary<string, StringValues> queryDictionary = QueryHelpers.ParseQuery(context.Request.QueryString.Value);
            IEnumerable<KeyValuePair<string, string>> sanitizedEntries = queryDictionary.SelectMany(
                kvp => kvp.Value,
                (kvp, value) => KeyValuePair.Create(
                    kvp.Key,
                    string.Equals(kvp.Key, "access_token", StringComparison.OrdinalIgnoreCase) ? "[REDACTED]" : (value ?? string.Empty)));

            QueryBuilder queryBuilder = new(sanitizedEntries);
            context.Request.QueryString = queryBuilder.ToQueryString();
        }

        await _next(context);
    }
}
