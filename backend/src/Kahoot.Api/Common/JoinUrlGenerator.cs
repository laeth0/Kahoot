using Kahoot.Application.Common.Interfaces;
using Kahoot.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Kahoot.Api.Common;

internal sealed class JoinUrlGenerator(
    IOptions<ClientOptions> clientOptions,
    IHttpContextAccessor httpContextAccessor) : IJoinUrlGenerator
{
    public string GenerateJoinUrl(string pin)
    {
        string? configuredBase = clientOptions.Value.BaseUrl?.TrimEnd('/');

        if (!string.IsNullOrWhiteSpace(configuredBase))
        {
            return $"{configuredBase}/join?pin={pin}";
        }

        if (httpContextAccessor.HttpContext is { Request: { } request })
        {
            string scheme = request.Scheme;
            string host = request.Host.ToUriComponent();
            return $"{scheme}://{host}/join?pin={pin}";
        }

        return $"/join?pin={pin}";
    }
}
