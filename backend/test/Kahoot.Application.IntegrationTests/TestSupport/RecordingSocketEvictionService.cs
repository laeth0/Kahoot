namespace Kahoot.Application.IntegrationTests.TestSupport;

using System.Collections.Concurrent;
using Kahoot.Application.Common.Interfaces;

public sealed record SocketEvictionRecord(Guid HostAccountId, CancellationToken CancellationToken);

public sealed class RecordingSocketEvictionService : ISocketEvictionService
{
    private readonly ConcurrentBag<SocketEvictionRecord> _records = new();

    public IReadOnlyList<SocketEvictionRecord> Records => _records.ToArray();

    public Func<SocketEvictionRecord, Task>? Callback { get; set; }

    public async Task EvictUserSocketsAsync(Guid hostAccountId, CancellationToken cancellationToken = default)
    {
        SocketEvictionRecord record = new(hostAccountId, cancellationToken);
        _records.Add(record);

        if (Callback is not null)
        {
            await Callback(record);
        }
    }
}
