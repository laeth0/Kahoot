namespace Kahoot.Application.IntegrationTests.TestSupport;

using System.Collections.Concurrent;
using Kahoot.Application.Common.Interfaces;

public sealed class RecordingSuspensionFinalizerChannel : ISuspensionFinalizerChannel
{
    private readonly ConcurrentBag<Guid> _suspendedAccounts = new();

    public IReadOnlyList<Guid> SuspendedAccounts => _suspendedAccounts.ToArray();
    public IReadOnlyList<Guid> Suspensions => SuspendedAccounts;

    public void NotifySuspension(Guid hostAccountId)
    {
        _suspendedAccounts.Add(hostAccountId);
    }

    public ValueTask<Guid> ReadAsync(CancellationToken cancellationToken)
    {
        throw new NotSupportedException("Worker consumption is not supported in application integration tests.");
    }

    public ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken)
    {
        throw new NotSupportedException("Worker consumption is not supported in application integration tests.");
    }

    public bool TryRead(out Guid hostAccountId)
    {
        throw new NotSupportedException("Worker consumption is not supported in application integration tests.");
    }
}
