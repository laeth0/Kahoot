namespace Kahoot.Infrastructure.UnitTests.TestSupport;

using System.Threading;

public sealed record RecordedSend(
    string Group,
    string Method,
    object?[] Args,
    CancellationToken CancellationToken)
{
    public object? SinglePayload => Args.Length > 0 ? Args[0] : null;
}
