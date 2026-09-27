namespace Kahoot.Application.Common.Interfaces;

public interface ISuspensionFinalizerChannel
{
    void NotifySuspension(Guid hostAccountId);

    ValueTask<Guid> ReadAsync(CancellationToken cancellationToken);

    ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken);

    bool TryRead(out Guid hostAccountId);
}
