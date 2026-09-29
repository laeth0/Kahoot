namespace Kahoot.Application.Common.Interfaces;

public interface ISuspensionFinalizerChannel
{
    // Non-Blocking Suspension Handoff (ADMIN-SUSP-002) - Writes suspended host ID to bounded channel for background finalization
    void NotifySuspension(Guid hostAccountId);

    // Asynchronous Queue Reader - Awaits next suspended account ID to process teardown
    ValueTask<Guid> ReadAsync(CancellationToken cancellationToken);

    // Channel Reader Readiness - Awaits readiness signal that items are available to read
    ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken);

    // Non-Blocking Read Try - Attempts synchronous read from channel buffer
    bool TryRead(out Guid hostAccountId);
}
