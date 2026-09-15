namespace Kahoot.Application.Games.Presence;

public interface IHostPresenceTracker
{
    void HostConnected(Guid gameId, string connectionId);

    void HostDisconnected(Guid gameId, string connectionId, Func<Guid, Task> onTimeout);

    bool IsHostConnectedOrInGracePeriod(Guid gameId);

    void RemoveGame(Guid gameId);
}
