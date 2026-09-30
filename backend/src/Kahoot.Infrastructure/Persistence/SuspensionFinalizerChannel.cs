using System.Threading.Channels;
using Kahoot.Application.Common.Interfaces;

namespace Kahoot.Infrastructure.Persistence;

internal sealed class SuspensionFinalizerChannel : ISuspensionFinalizerChannel
{
    private readonly Channel<Guid> _channel;

    public SuspensionFinalizerChannel()
    {
        // ====================================================================================================
        // BACKPRESSURE PATTERN: Bounded Producer-Consumer Channel (System.Threading.Channels)
        // ----------------------------------------------------------------------------------------------------
        // Context / Problem:
        // When accounts are suspended, notifications trigger background cleanup. During high-volume bursts or
        // bulk administrator operations, an unbounded channel would accumulate items indefinitely in memory,
        // causing memory bloat and potential Out-Of-Memory (OOM) failures under sustained load.
        //
        // Approach & Implementation (ADMIN-SUSP-002):
        // 1. Bounded Buffer: Capped at 1024 items (BoundedChannelOptions(1024)).
        // 2. DropWrite FullMode: When the buffer reaches 1024 items, new writes are non-blockingly dropped
        //    (BoundedChannelFullMode.DropWrite) rather than blocking the caller or throwing an exception.
        // 3. Loss-Tolerant Eventual Consistency: This in-memory channel acts only as a low-latency "wake-up hint"
        //    for SuspensionFinalizerWorker. The worker also runs a periodic PostgreSQL sweep (SweepInterval = 1m),
        //    guaranteeing eventual consistency for all suspended accounts even if wake-up signals are dropped.
        // ====================================================================================================
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
