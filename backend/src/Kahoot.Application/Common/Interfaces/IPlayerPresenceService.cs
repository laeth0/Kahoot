namespace Kahoot.Application.Common.Interfaces;

public interface IPlayerPresenceService
{
    Task<bool> HasActiveConnectionAsync(Guid participantId, CancellationToken cancellationToken);

    Task<int> GetConnectedCountAsync(Guid gameId);

    Task EvictParticipantAsync(Guid participantId, Guid gameId, CancellationToken cancellationToken);
}
