using Kahoot.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace Kahoot.Infrastructure.Realtime;

internal sealed class SocketEvictionService : ISocketEvictionService
{
    private readonly ILogger<SocketEvictionService> _logger;

    public SocketEvictionService(ILogger<SocketEvictionService> logger)
    {
        _logger = logger;
    }

    public Task EvictUserSocketsAsync(Guid hostAccountId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Socket eviction requested for host account. EventName={EventName} HostAccountId={HostAccountId}",
            "SocketEvictionRequested",
            hostAccountId);

        return Task.CompletedTask;
    }
}
