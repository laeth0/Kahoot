namespace Kahoot.Infrastructure.UnitTests.TestSupport;

public sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset _utcNow;

    public ManualTimeProvider(DateTimeOffset initialUtcNow)
    {
        _utcNow = initialUtcNow.ToUniversalTime();
    }

    public ManualTimeProvider()
        : this(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero))
    {
    }

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void Advance(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "Cannot advance backward in time.");
        }

        _utcNow = _utcNow.Add(duration);
    }

    public void SetUtcNow(DateTimeOffset utcNow)
    {
        _utcNow = utcNow.ToUniversalTime();
    }
}
