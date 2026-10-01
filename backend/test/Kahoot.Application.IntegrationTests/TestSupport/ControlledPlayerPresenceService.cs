namespace Kahoot.Application.IntegrationTests.TestSupport;

using System.Collections.Concurrent;
using Kahoot.Application.Common.Interfaces;

public sealed record EvictedParticipantRecord(
    Guid ParticipantId,
    Guid GameId,
    long StateVersion,
    CancellationToken CancellationToken);

public sealed class ControlledPlayerPresenceService : IPlayerPresenceService
{
    private readonly ConcurrentDictionary<Guid, bool> _activeConnections = new();
    private readonly ConcurrentBag<EvictedParticipantRecord> _evictions = new();

    public IReadOnlyList<EvictedParticipantRecord> Evictions => _evictions.ToArray();

    public Func<EvictedParticipantRecord, Task>? Callback { get; set; }

    public void SetActive(Guid participantId, bool isActive)
    {
        _activeConnections[participantId] = isActive;
    }

    public Task<bool> HasActiveConnectionAsync(Guid participantId, CancellationToken cancellationToken)
    {
        bool isActive = _activeConnections.TryGetValue(participantId, out bool active) && active;
        return Task.FromResult(isActive);
    }

    public Task<int> GetConnectedCountAsync(Guid gameId)
    {
        return Task.FromResult(0);
    }

    public async Task EvictParticipantAsync(
        Guid participantId,
        Guid gameId,
        long stateVersion,
        CancellationToken cancellationToken)
    {
        EvictedParticipantRecord record = new(participantId, gameId, stateVersion, cancellationToken);
        _evictions.Add(record);

        if (Callback is not null)
        {
            await Callback(record);
        }
    }
}
