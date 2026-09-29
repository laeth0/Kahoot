using System.Threading.Channels;
using Kahoot.Application.Common.Interfaces;

namespace Kahoot.Infrastructure.Persistence;

internal sealed class SuspensionFinalizerChannel : ISuspensionFinalizerChannel
{
    private readonly Channel<Guid> _channel;

    public SuspensionFinalizerChannel()
    {
        // Bounded Backpressure Configuration (ADMIN-SUSP-002) - Limits buffer to 1024 items with DropWrite mode to prevent memory exhaustion
        BoundedChannelOptions options = new BoundedChannelOptions(1024)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropWrite
        };

        // Latency Optimization Handoff - Channel signals prompt wake-up; persistent DB sweep guarantees eventual consistency
        _channel = Channel.CreateBounded<Guid>(options);
    }

    // Non-Blocking Hint Notification - Emits non-blocking signal to wake up background finalizer worker
    public void NotifySuspension(Guid hostAccountId)
    {
        _channel.Writer.TryWrite(hostAccountId);
    }

    public ValueTask<Guid> ReadAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAsync(cancellationToken);
    }

    public ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.WaitToReadAsync(cancellationToken);
    }

    public bool TryRead(out Guid hostAccountId)
    {
        return _channel.Reader.TryRead(out hostAccountId);
    }
}
