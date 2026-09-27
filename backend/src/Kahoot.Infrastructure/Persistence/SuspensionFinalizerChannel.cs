using System.Threading.Channels;
using Kahoot.Application.Common.Interfaces;

namespace Kahoot.Infrastructure.Persistence;

internal sealed class SuspensionFinalizerChannel : ISuspensionFinalizerChannel
{
    private readonly Channel<Guid> _channel;

    public SuspensionFinalizerChannel()
    {
        UnboundedChannelOptions options = new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        };

        _channel = Channel.CreateUnbounded<Guid>(options);
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
