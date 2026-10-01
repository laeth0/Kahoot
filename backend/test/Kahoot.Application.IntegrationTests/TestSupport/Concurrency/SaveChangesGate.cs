namespace Kahoot.Application.IntegrationTests.TestSupport.Concurrency;

using System;
using System.Threading;
using System.Threading.Tasks;

public sealed class SaveChangesGate
{
    private readonly TaskCompletionSource _reachedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _releaseTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Reached => _reachedTcs.Task;

    public void SignalReached()
    {
        _reachedTcs.TrySetResult();
    }

    public void Release()
    {
        _releaseTcs.TrySetResult();
    }

    public async Task AwaitReleaseAsync(CancellationToken cancellationToken = default)
    {
        using (cancellationToken.Register(() => _releaseTcs.TrySetCanceled(cancellationToken)))
        {
            await _releaseTcs.Task;
        }
    }
}
