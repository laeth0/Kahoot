namespace Kahoot.Application.Common.Interfaces;

public interface IPlayerPresenceService
{
    // Active Connection Check (PRES-PLAY-001) - Queries distributed Redis presence keys to verify live participant socket
    Task<bool> HasActiveConnectionAsync(Guid participantId, CancellationToken cancellationToken);

    // Connected Count Aggregation - Returns total active connected participants for game lobby
    Task<int> GetConnectedCountAsync(Guid gameId);

    // Participant Socket Eviction (LOBBY-EVICT-001) - Clears Redis presence key and forcefully disconnects participant hub socket
    Task EvictParticipantAsync(Guid participantId, Guid gameId, long stateVersion, CancellationToken cancellationToken);
}
