namespace Kahoot.Application.Common.Interfaces;

public interface ISocketEvictionService
{
    Task EvictUserSocketsAsync(Guid hostAccountId, CancellationToken cancellationToken = default);
}
