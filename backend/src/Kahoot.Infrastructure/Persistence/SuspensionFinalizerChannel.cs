using System.Threading.Channels;
using Kahoot.Application.Common.Interfaces;

namespace Kahoot.Infrastructure.Persistence;

internal sealed class SuspensionFinalizerChannel : ISuspensionFinalizerChannel
{
    private readonly Channel<Guid> _channel;

    public SuspensionFinalizerChannel()
    {
        BoundedChannelOptions options = new BoundedChannelOptions(1024)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropWrite
        };

        // Notifications only reduce latency; the database sweep recovers dropped hints.
        _channel = Channel.CreateBounded<Guid>(options);
    }

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
