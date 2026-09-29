namespace Kahoot.Application.Common.Interfaces;

public interface ISocketEvictionService
{
    // Cluster-Wide Socket Eviction (ADMIN-EVICT-001) - Publishes eviction message to Redis backplane to disconnect all host sockets
    Task EvictUserSocketsAsync(Guid hostAccountId, CancellationToken cancellationToken = default);
}
